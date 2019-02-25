namespace AgDatabaseMove.SmoFacade
{
  using System;
  using System.Collections.Generic;
  using System.Data.SqlClient;
  using System.Linq;
  using System.Threading.Tasks;


  internal interface IListener : IDisposable
  {
    /// <summary>
    ///   The primary instance at the time of construction.
    /// </summary>
    Server Primary { get; set; }

    /// <summary>
    ///   The secondary instances at the time of construction.
    /// </summary>
    IEnumerable<Server> Secondaries { get; set; }

    /// <summary>
    ///   The availability group associated with the supplied listener.
    /// </summary>
    AvailabilityGroup AvailabilityGroup { get; set; }

    /// <summary>
    ///   Executes the provided action on each availability group server instance.
    ///   This may use one or more threads to execute in parallel.
    /// </summary>
    void ForEachAgInstance(Action<Server, AvailabilityGroup> action);

    /// <summary>
    ///   Executes the provided action on each availability group server instance.
    ///   This may use one or more threads to execute in parallel.
    /// </summary>
    void ForEachAgInstance(Action<Server> action);
  }

  internal class Listener : IListener
  {
    public Listener(string connectionString)
    {
      /*
       * We initially connect an availability group instance by way of a listener name. This creates a different connection and
       * SMO server object from connecting directly to the primary instance. Having different SMO server objects results in the
       * data cached by SMO getting out of sync between the two and forces us to refresh the objects regularly. This should
       * prevent us from having to do the refreshes by only using SMO objects that connect directly to the server. The initial
       * connection to find those server names though is done through the listener and that instance is thrown away in this
       * constructor so it won't be used again.
       */

      var connectionStringBuilder = new SqlConnectionStringBuilder(connectionString);
      connectionStringBuilder.InitialCatalog = "master";
      if(connectionStringBuilder.DataSource == null)
        throw new Exception("DataSource not supplied in connection string.");
      using(var server = new Server(connectionStringBuilder.ToString())) {
        // Find the AG associated with the listener
        var availabilityGroup =
          server.AvailabilityGroups.Single(ag =>
                                             ag.Listeners.Contains(AgListenerName(connectionStringBuilder.DataSource),
                                                                   StringComparer.InvariantCultureIgnoreCase));

        // List out the servers in the AG
        var primaryName = availabilityGroup.PrimaryInstance;
        var secondaryNames = availabilityGroup.Replicas.Where(l => l != primaryName);

        // Connect to each server instance
        connectionStringBuilder.DataSource = primaryName;
        Primary = new Server(connectionStringBuilder.ToString());
        var secondaryServers = new List<Server>();
        foreach(var secondaryName in secondaryNames) {
          connectionStringBuilder.DataSource = secondaryName;
          secondaryServers.Add(new Server(connectionStringBuilder.ToString()));
        }

        Secondaries = secondaryServers;

        AvailabilityGroup = Primary.AvailabilityGroups.Single(ag => ag.Name == availabilityGroup.Name);
      }
    }

    public IEnumerable<Server> ReplicaInstances => Secondaries.Union(new[] { Primary });

    // TODO: This could change due to fail over, so we may want to build a better accessor here.
    public Server Primary { get; set; }

    public IEnumerable<Server> Secondaries { get; set; }

    public AvailabilityGroup AvailabilityGroup { get; set; }

    public void Dispose()
    {
      Primary?.Dispose();
      if(Secondaries != null) {
        foreach(var secondary in Secondaries)
          secondary?.Dispose();
        Secondaries = null;
      }
    }


    public void ForEachAgInstance(Action<Server> action)
    {
      ForEachAgInstance((s, ag) => action(s));
    }

    /// <summary>
    ///   Executes the action on each server that is a member of the availability group.
    /// </summary>
    public void ForEachAgInstance(Action<Server, AvailabilityGroup> action)
    {
      // https://docs.microsoft.com/en-us/dotnet/standard/parallel-programming/potential-pitfalls-with-plinq#do-not-assume-that-iterations-of-foreach-for-and-forall-always-execute-in-parallel
      // I was thinking about ensuring the primary gets joined first by trying to synchronize in this action. Don't do that.
      Parallel.ForEach(ReplicaInstances, ri => InvokeOnReplica(ri, AvailabilityGroup.Name, action));
    }

    private static string AgListenerName(string dataSource)
    {
      var dotIndex = dataSource.IndexOf('.');
      return dotIndex >= 0 ? dataSource.Remove(dotIndex) : dataSource;
    }


    /// <summary>
    ///   Connects to a given replica and executes the action.
    /// </summary>
    /// <param name="replica">The name of the replica instance.</param>
    /// <param name="AgName">Name of the availability group</param>
    /// <param name="action">The action to execute.</param>
    private void InvokeOnReplica(Server replica, string AgName, Action<Server, AvailabilityGroup> action)
    {
      try {
        action.Invoke(replica, replica.AvailabilityGroups.Single(ag => ag.Name == AgName));
      }
      catch(Exception e) {
        throw e;
      }
    }
  }
}
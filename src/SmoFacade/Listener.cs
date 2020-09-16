namespace AgDatabaseMove.SmoFacade
{
  using System;
  using System.Collections.Generic;
  using System.Data.SqlClient;
  using System.Linq;
  using System.Net;
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
    IEnumerable<Server> Secondaries { get; }

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
    private IList<Server> _secondaries;

    /**
     * We initially connect an availability group instance by way of a listener name. This creates a different connection and
     * SMO server object from connecting directly to the primary instance. Having different SMO server objects results in the
     * data cached by SMO getting out of sync between the two and forces us to refresh the objects regularly. This should
     * prevent us from having to do the refreshes by only using SMO objects that connect directly to the server. The initial
     * connection to find those server names though is done through the listener and that instance is thrown away in this
     * constructor so it won't be used again.
     */
    public Listener(SqlConnectionStringBuilder connectionStringBuilder)
    {
      if(connectionStringBuilder.DataSource == null)
        throw new ArgumentException("DataSource not supplied in connection string");

      connectionStringBuilder.InitialCatalog = "master";

      using var server = new Server(connectionStringBuilder.ToString());
      // Find the AG associated with the listener
      var availabilityGroup =
        server.AvailabilityGroups.Single(ag =>
                                           ag.Listeners.Contains(AgListenerName(connectionStringBuilder.DataSource),
                                                                 StringComparer.InvariantCultureIgnoreCase));

      // List out the servers in the AG
      var primaryName = availabilityGroup.PrimaryInstance;
      var secondaryNames = availabilityGroup.Replicas.Where(l => l != primaryName);

      // Connect to each server instance
      Primary = AgListenerNameToServer(ref connectionStringBuilder, primaryName);
      AvailabilityGroup = Primary.AvailabilityGroups.Single(ag => ag.Name == availabilityGroup.Name);

      _secondaries = new List<Server>();
      foreach(var secondaryName in secondaryNames)
        _secondaries.Add(AgListenerNameToServer(ref connectionStringBuilder, secondaryName));
    }

    public IEnumerable<Server> ReplicaInstances => Secondaries.Union(new[] { Primary });

    // TODO: This could change due to fail over, so we may want to build a better accessor here.
    public Server Primary { get; set; }

    public IEnumerable<Server> Secondaries => _secondaries;

    public AvailabilityGroup AvailabilityGroup { get; set; }

    public void Dispose()
    {
      Primary?.Dispose();
      if(_secondaries != null) {
        foreach(var secondary in _secondaries) secondary?.Dispose();

        _secondaries = null;
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

    /// <summary>
    ///   Connects to a given replica and executes the action.
    /// </summary>
    /// <param name="replica">The name of the replica instance.</param>
    /// <param name="AgName">Name of the availability group</param>
    /// <param name="action">The action to execute.</param>
    private void InvokeOnReplica(Server replica, string AgName, Action<Server, AvailabilityGroup> action)
    {
      action.Invoke(replica, replica.AvailabilityGroups.Single(ag => ag.Name == AgName));
    }

    private static string AgListenerName(string dataSource)
    {
      var dotIndex = dataSource.IndexOf('.');
      return dotIndex >= 0 ? dataSource.Remove(dotIndex) : dataSource;
    }

    private static Server AgListenerNameToServer(ref SqlConnectionStringBuilder connBuilder, string agInstanceName)
    {
      connBuilder.DataSource = Dns.GetHostEntry(agInstanceName).HostName;
      return new Server(connBuilder.ToString());
    }
  }
}
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
    public Listener(SqlConnectionStringBuilder connectionStringBuilder, string credentialName = null)
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
      Primary = AgInstanceNameToServer(ref connectionStringBuilder, primaryName, credentialName);
      AvailabilityGroup = Primary.AvailabilityGroups.Single(ag => ag.Name == availabilityGroup.Name);

      _secondaries = new List<Server>();
      foreach(var secondaryName in secondaryNames)
        _secondaries.Add(AgInstanceNameToServer(ref connectionStringBuilder,
                                                secondaryName,
                                                credentialName));
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

    private static Server AgInstanceNameToServer(ref SqlConnectionStringBuilder connBuilder, string agInstanceName,
      string credentialName)
    {
      try 
      {
        connBuilder.DataSource = ResolveDnsHostNameForInstance(agInstanceName, connBuilder.DataSource);
        return new Server(connBuilder.ToString(), credentialName);
      }
      catch (Exception e)
      {
        throw new ArgumentException($"agInstanceName param {agInstanceName} cannot be resolved by DNS", e);
      }
    }

    /// <summary>
    ///   Resolves 'agReplicaInstanceName' to a FQDN
    ///   However on Unix OS, when 'val' in 'Dns.GetHostEntry(val)' is not a FQDN, it fails intermittently
    ///   Therefore, if dns lookup on just the instance name fails, we retry after appending the domain fragments from the listener to the instance name
    /// </summary>
    /// <param name="agReplicaInstanceName"> The name for an instance within the AG (for which we are trying to get the FQDN)</param>
    /// <param name="agListenerDomain"> The complete domain for the AG listener</param>
    private static string ResolveDnsHostNameForInstance(string agReplicaInstanceName, string agListenerDomain)
    {
      // Sometimes instances and listeners have ports or named instances
      // Therefore, we strip them off before calling DNS.GetHostEntry() and then add them back to the result 
      var (listenerDomain, listenerPortOrNamedInstance) = SplitDomainAndPort(agListenerDomain);
      var (instanceName, instancePortOrNamedInstance) = SplitDomainAndPort(agReplicaInstanceName);
      var preferredPortOrNamedInstance = GetPreferredPort(instancePortOrNamedInstance, listenerPortOrNamedInstance);

      try
      {
        return $"{Dns.GetHostEntry(instanceName).HostName}{preferredPortOrNamedInstance}";
      }
      catch (System.Net.Sockets.SocketException)
      {
        // Re-try by appending the domain fragments from listener to the instance name 
        // However, we don't need the listener's "host name" (first fragment), so we need to strip that off
        // eg: if listener is "abc.def.ghi" we want to append only ".def.ghi" to the instance name
        var listenerDomainFragments = listenerDomain.Split('.');
        listenerDomainFragments[0] = null;
        var instanceDomain = $"{instanceName}{string.Join(".", listenerDomainFragments)}";

        return $"{Dns.GetHostEntry(instanceDomain).HostName}{preferredPortOrNamedInstance}";
      }
    }

    // First preference is to add back a port over a named instance  
    // (port is TCP/IP standard while named instance is only SQL Server standard)
    // If both are same type, then prioritize instance over listener (in almost all cases they should be identical)
    internal static string GetPreferredPort(string instancePortOrNamedInstance, string listenerPortOrNamedInstance)
    {
      if (string.IsNullOrEmpty(instancePortOrNamedInstance) || string.IsNullOrEmpty(listenerPortOrNamedInstance))
      {
        return (instancePortOrNamedInstance ?? listenerPortOrNamedInstance);
      }
      return instancePortOrNamedInstance.StartsWith("\\") && listenerPortOrNamedInstance.StartsWith(",") 
        ? listenerPortOrNamedInstance 
        : instancePortOrNamedInstance;
    }

    // This function handles named instances ("<domain>\<named instance>") in the same way as ports
    internal static (string domain, string port) SplitDomainAndPort(string domainAndPort)
    {
      var domain = domainAndPort;
      var splitValue = domainAndPort.Contains(',') ? "," : domainAndPort.Contains('\\') ? "\\" : null;

      if(splitValue == null)
      {
        return (domain, null);
      }

      var fragments = domainAndPort.Split(splitValue.ToCharArray(), 2);
      domain = fragments[0];
      var port = $"{splitValue}{fragments[1]}";

      return (domain, port);
    }

  }
}
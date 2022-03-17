namespace AgDatabaseMove.SmoFacade
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Polly;
  using MsftSmo = Microsoft.SqlServer.Management.Smo;


  /// <summary>
  ///   Adds some better accessors and simplifies some interactions with SMO's AvailabilityGroup object.
  /// </summary>
  public class AvailabilityGroup
  {
    private readonly MsftSmo.AvailabilityGroup _availabilityGroup;

    internal AvailabilityGroup(MsftSmo.AvailabilityGroup availabilityGroup)
    {
      _availabilityGroup = availabilityGroup;
    }

    public bool IsPrimaryInstance =>
      _availabilityGroup.PrimaryReplicaServerName.Equals(_availabilityGroup.Parent.NetName,
                                                         StringComparison.InvariantCultureIgnoreCase);

    public string PrimaryInstance => _availabilityGroup.PrimaryReplicaServerName;
    public string Name => _availabilityGroup.Name;

    public IEnumerable<string> Listeners => _availabilityGroup.AvailabilityGroupListeners
      .Cast<MsftSmo.AvailabilityGroupListener>()
      .Select(agl => agl.Name.ToString());

    public IEnumerable<string> Replicas =>
      _availabilityGroup.AvailabilityReplicas.Cast<MsftSmo.AvailabilityReplica>().Select(ar => ar.Name);

    public IEnumerable<string> Databases =>
      _availabilityGroup.AvailabilityDatabases.Cast<MsftSmo.AvailabilityDatabase>().Select(d => d.Name);

    public void JoinSecondary(string dbName)
    {
      var agDb = Policy
          .HandleResult<MsftSmo.AvailabilityDatabase>(r => r == null)
          .WaitAndRetry(4, retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(10, retryAttempt)))
          .Execute(() => { _availabilityGroup.AvailabilityDatabases.Refresh(); return _availabilityGroup.AvailabilityDatabases[dbName]; }); 
      
      agDb.JoinAvailablityGroup();
    }

    public void JoinPrimary(string dbName)
    {
      var availabilityGroupDb = new MsftSmo.AvailabilityDatabase(_availabilityGroup, dbName);
      availabilityGroupDb.Create();
    }

    public void Remove(string dbName)
    {
      _availabilityGroup.AvailabilityDatabases[dbName]?.Drop();
    }

    public bool IsInitializing(string dbName)
    {
      // The availability database needs to be refreshed since the state changes on the server side.
      var availabilityDatabase = _availabilityGroup.AvailabilityDatabases.Cast<MsftSmo.AvailabilityDatabase>()
        .SingleOrDefault(d => d.Name.Equals(dbName, StringComparison.InvariantCultureIgnoreCase));
      if(availabilityDatabase == null)
        return false;
      availabilityDatabase.Refresh();

      return availabilityDatabase.SynchronizationState == MsftSmo.AvailabilityDatabaseSynchronizationState.Initializing;
    }
  }
}

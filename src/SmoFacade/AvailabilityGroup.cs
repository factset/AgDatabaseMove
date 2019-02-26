﻿namespace AgDatabaseMove.SmoFacade
{
  using System.Collections.Generic;
  using System.Linq;
  using Smo = Microsoft.SqlServer.Management.Smo;


  /// <summary>
  ///   Adds some better accessors and simplifies some interactions with SMO's AvailabilityGroup object.
  /// </summary>
  public class AvailabilityGroup
  {
    private readonly Smo.AvailabilityGroup _availabilityGroup;

    internal AvailabilityGroup(Smo.AvailabilityGroup availabilityGroup)
    {
      _availabilityGroup = availabilityGroup;
    }

    public bool IsPrimaryInstance => _availabilityGroup.PrimaryReplicaServerName == _availabilityGroup.Parent.Name;
    public string PrimaryInstance => _availabilityGroup.PrimaryReplicaServerName;
    public string Name => _availabilityGroup.Name;

    public IEnumerable<string> Listeners => _availabilityGroup.AvailabilityGroupListeners
      .Cast<Smo.AvailabilityGroupListener>().Select(agl => agl.Name.ToString());

    public IEnumerable<string> Replicas =>
      _availabilityGroup.AvailabilityReplicas.Cast<Smo.AvailabilityReplica>().Select(ar => ar.Name);

    public IEnumerable<string> Databases =>
      _availabilityGroup.AvailabilityDatabases.Cast<Smo.AvailabilityDatabase>().Select(d => d.Name);

    public void JoinSecondary(string dbName)
    {
      _availabilityGroup.AvailabilityDatabases[dbName].JoinAvailablityGroup();
    }

    public void JoinPrimary(string dbName)
    {
      var availabilityGroupDb = new Smo.AvailabilityDatabase(_availabilityGroup, dbName);
      availabilityGroupDb.Create();
    }

    public void Remove(string dbName)
    {
      _availabilityGroup.AvailabilityDatabases[dbName]?.Drop();
    }

    public bool IsInitializing(string dbName)
    {
      // The availability database needs to be refreshed since the state changes on the server side.
      var availabilityDatabase = _availabilityGroup.AvailabilityDatabases.Cast<Smo.AvailabilityDatabase>()
        .SingleOrDefault(d => d.Name == dbName);
      if(availabilityDatabase == null)
        return false;
      availabilityDatabase.Refresh();

      return availabilityDatabase.SynchronizationState == Smo.AvailabilityDatabaseSynchronizationState.Initializing;
    }
  }
}
using System.Runtime.CompilerServices;


[assembly: InternalsVisibleTo("AgDatabaseMove.Integration")]

namespace AgDatabaseMove
{
  using System;
  using System.Collections.Concurrent;
  using System.Collections.Generic;
  using System.Linq;
  using System.Threading;
  using SmoFacade;


  /// <summary>
  ///   A connection to the primary instance of an availability group referencing a database name.
  ///   The database does not have to exist or be a part of the availability group, and can be created or added to the AG via
  ///   this interface.
  /// </summary>
  public class AgDatabase : IDisposable, IAgDatabase
  {
    private readonly string _backupPathTemplate;
    internal readonly IListener _listener;

    /// <summary>
    ///   A constructor that uses a config object for more options.
    /// </summary>
    /// <param name="dbConfig">A DatabaseConfig where the DataSource is the AG listener.</param>
    public AgDatabase(DatabaseConfig dbConfig)
    {
      _listener = new Listener(dbConfig.ConnectionString);
      Name = dbConfig.DatabaseName;
      _backupPathTemplate = dbConfig.BackupPathTemplate;
    }

    /// <summary>
    ///   Determines if the database is in a restoring state.
    /// </summary>
    public bool Restoring => _listener.Primary.Database(Name)?.Restoring ?? false;

    /// <summary>
    ///   Database name
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///   Determines if the database exists.
    /// </summary>
    public bool Exists()
    {
      return _listener.Primary.Database(Name) != null;
    }

    /// <summary>
    ///   Removes the database from the AG and deletes it from all instances.
    /// </summary>
    public void Delete()
    {
      // Deleting the database while it is initializing will leave it in a state where system redo threads are stuck.
      // This leaves the database in a state that a SQL Server service restart prior to deletion.
      _listener.ForEachAgInstance(WaitForInitialization);
      _listener.AvailabilityGroup.Remove(Name);
      _listener.ForEachAgInstance(s => s.Database(Name)?.Drop());
    }

    /// <summary>
    ///   Takes a log backup on the primary instance.
    /// </summary>
    public void LogBackup()
    {
      _listener.Primary.LogBackup(Name, _backupPathTemplate);
    }

    /// <summary>
    ///   Restores the database backups to each instance in the AG.
    ///   We suggest using <see cref="AgDatabaseMove.Restore" /> to assist with restores.
    /// </summary>
    /// <param name="backupOrder">An ordered list of backups to restore.</param>
    /// <param name="fileRelocation">A method to generate the new file location when moving the database.</param>
    public void Restore(IEnumerable<BackupMetadata> backupOrder,
      Func<string, string> fileRelocation = null)
    {
      _listener.ForEachAgInstance(s => s.Restore(backupOrder, Name, fileRelocation));
    }

    /// <summary>
    ///   Builds a list of recent backups from msdb on each AG instance.
    /// </summary>
    public List<BackupMetadata> RecentBackups()
    {
      var bag = new ConcurrentBag<BackupMetadata>();
      _listener.ForEachAgInstance(s => s.Database(Name).RecentBackups()
                                    .ForEach(bu => bag.Add(bu)));
      return bag.ToList();
    }

    /// <summary>
    ///   Joins the database to the AG on each instance.
    /// </summary>
    public void JoinAg()
    {
      FinalizePrimary();
      _listener.ForEachAgInstance((s, ag) => {
        if(ag.IsPrimaryInstance)
          ag.JoinPrimary(Name);
      });
      _listener.ForEachAgInstance((s, ag) => {
        if(!ag.IsPrimaryInstance) ag.JoinSecondary(Name);
      });
    }

    public void CopyLogins(IEnumerable<LoginProperties> logins)
    {
      _listener.ForEachAgInstance(server => server.EnsureLogins(logins));
    }

    public IEnumerable<LoginProperties> AssociatedLogins()
    {
      return _listener.Primary.Database(Name).Users.Where(u => u.Login != null).Select(u => u.Login.Properties());
    }

    /// <summary>
    ///   IDisposable implemented for our connection to the primary AG database server.
    /// </summary>
    public void Dispose()
    {
      _listener?.Dispose();
    }

    private void WaitForInitialization(Server server, AvailabilityGroup availabilityGroup)
    {
      var wait = 100;
      var maxWait = 60000;
      var multiplier = 2;

      while(availabilityGroup.IsInitializing(Name)) {
        if(wait > maxWait)
          throw new TimeoutException($"{server.Name} is initializing. Wait period expired.");
        Thread.Sleep(wait);
        wait *= multiplier;
      }
    }

    public void FinalizePrimary()
    {
      _listener.ForEachAgInstance(FinalizePrimary);
    }

    private void FinalizePrimary(Server server, AvailabilityGroup availabilityGroup)
    {
      if(!availabilityGroup.IsPrimaryInstance)
        return;

      var database = server.Database(Name);
      if(!database.Restoring)
        return;

      database.RestoreWithRecovery();
    }

    public bool IsInitializing()
    {
      var result = 0;
      _listener.ForEachAgInstance((s, ag) => {
        if(ag.IsInitializing(Name))
          Interlocked.Increment(ref result);
      });
      return result > 0;
    }
  }
}
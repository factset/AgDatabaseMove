namespace AgDatabaseMove.SmoFacade
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using Microsoft.SqlServer.Management.Smo;
  using Polly;


  /// <summary>
  ///   Adds some better accessors and simplifies some interactions with SMO's database class.
  /// </summary>
  public class Database
  {
    private readonly Microsoft.SqlServer.Management.Smo.Database _database;
    private readonly Server _server;

    internal Database(Microsoft.SqlServer.Management.Smo.Database database, Server server)
    {
      _database = database;
      _server = server;
    }

    public IEnumerable<User> Users => _database.Users.Cast<Microsoft.SqlServer.Management.Smo.User>()
      .Select(u => new User(u, _server));

    public string Name => _database.Name;

    public bool Restoring => _database.Status == DatabaseStatus.Restoring;

    public void SizeMb()
    {
      _server.DatabaseSizeMb(Name);
    }

    public void RestoreWithRecovery()
    {
      // TODO: Sql injection paranoia. - can we execute normally with a parameterized statement here?
      _database.Parent.Databases["master"].ExecuteNonQuery($"RESTORE DATABASE [{_database.Name}] WITH RECOVERY");
      _database.Refresh();
    }

    public void DropAssociatedLogins()
    {
      var logins = Users.Where(u => u.Login != null && u.Login.Name != "sa")
        .Select(u => u.Login.Properties());
      foreach(var login in logins) {
        _server.DropLogin(new LoginProperties() {
          Name = login.Name
        });
      }
    }

    /// <summary>
    ///   Kills connections and deletes the database.
    /// </summary>
    public void Drop()
    {
      ThrowIfUnreadyToDrop();

      var policy = Policy
        .Handle<FailedOperationException>()
        .WaitAndRetry(3, retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(10, retryAttempt)));

      policy.Execute(() => { _database.Refresh(); _database.Parent.KillDatabase(_database.Name); });
    }

    /// <summary>
    ///   Queries msdb on the instance for backups of this database.
    /// </summary>
    /// <returns>A list of backups known by msdb</returns>
    public List<BackupMetadata> RecentBackups()
    {
      var backups = new List<BackupMetadata>();

      var query = "SELECT s.database_name, m.physical_device_name, s.backup_start_date, s.first_lsn, s.last_lsn," +
                  "s.database_backup_lsn, s.checkpoint_lsn, s.[type] AS backup_type, s.server_name, s.recovery_model " +
                  "FROM msdb.dbo.backupset s " +
                  "INNER JOIN msdb.dbo.backupmediafamily m ON s.media_set_id = m.media_set_id " +
                  "WHERE s.last_lsn >= (" +
                  "SELECT MAX(last_lsn) FROM msdb.dbo.backupset " +
                  "WHERE [type] = 'D' " +
                  "AND database_name = @dbName " +
                  "AND is_copy_only = 0" +
                  ") " +
                  "AND s.database_name = @dbName " +
                  "AND is_copy_only = 0 " +
                  "ORDER BY s.backup_start_date DESC, backup_finish_date";

      using var cmd = _server.SqlConnection.CreateCommand();
      cmd.CommandText = query;
      var dbName = cmd.CreateParameter();
      dbName.ParameterName = "dbName";
      dbName.Value = _database.Name;
      cmd.Parameters.Add(dbName);

      using var reader = cmd.ExecuteReader();
      while(reader.Read())
        backups.Add(new BackupMetadata {
          CheckpointLsn = (decimal)reader["checkpoint_lsn"],
          DatabaseBackupLsn = (decimal)reader["database_backup_lsn"],
          DatabaseName = (string)reader["database_name"],
          FirstLsn = (decimal)reader["first_lsn"],
          LastLsn = (decimal)reader["last_lsn"],
          PhysicalDeviceName = (string)reader["physical_device_name"],
          ServerName = (string)reader["server_name"],
          StartTime = (DateTime)reader["backup_start_date"],
          BackupType = BackupFileTools.BackupTypeAbbrevToType((string)reader["backup_type"])
        });

      return backups;
    }

    public void SingleUserMode()
    {
      _database.DatabaseOptions.UserAccess = DatabaseUserAccess.Single;
      _database.Alter(TerminationClause.RollbackTransactionsImmediately);
    }

    public void RestrictedUserMode()
    {
      _database.DatabaseOptions.UserAccess = DatabaseUserAccess.Restricted;
      _database.Alter(TerminationClause.RollbackTransactionsImmediately);
    }

    public void MultiUserMode()
    {
      _database.DatabaseOptions.UserAccess = DatabaseUserAccess.Multiple;
      _database.Alter(TerminationClause.RollbackTransactionsImmediately);
    }

    private void ThrowIfUnreadyToDrop()
    {
      // Deleting the database while it is initializing will leave it in a state where system redo threads are stuck.
      // This leaves the database in a state that a SQL Server service restart prior to deletion.

      var policyPrep = Policy
        .Handle<Exception>()
        .WaitAndRetry(6, retryAttempt => TimeSpan.FromMilliseconds(Math.Pow(5, retryAttempt)));

      // ensure database is not in AvailabilityGroup, WaitAndRetry loop for each instance to sync
      policyPrep.Execute(() => {
        _database.Refresh();
        if(Restoring)
          return; // restoring state means we're good to drop
        
        try {
          if(!string.IsNullOrEmpty(_database.AvailabilityGroupName))
            throw
              new Exception($"Cannot kill the database {Name} until it has been removed from the AvailabilityGroup");
          if(_database.AvailabilityDatabaseSynchronizationState >
             AvailabilityDatabaseSynchronizationState.NotSynchronizing)
            throw new
              Exception($"Cannot kill the database {Name} until AvailabilityDatabaseSynchronizationState is inaccessible");
        }
        catch(
          PropertyCannotBeRetrievedException) { } // good here, AvailabilityDatabaseSynchronizationState unavailable means it has no state
        catch(SmoException e) when(e.Message.Contains("Cannot access properties or methods")) { }
      });
    }
  }
}

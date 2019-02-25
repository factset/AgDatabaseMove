namespace AgDatabaseMove.SmoFacade
{
  using System.Collections.Generic;
  using System.Linq;
  using Smo = Microsoft.SqlServer.Management.Smo;


  /// <summary>
  ///   Adds some better accessors and simplifies some interactions with SMO's database class.
  /// </summary>
  public class Database
  {
    private readonly Smo.Database _database;
    private readonly Server _server;

    internal Database(Smo.Database database, Server server)
    {
      _database = database;
      _server = server;
    }

    public IEnumerable<User> Users => _database.Users.Cast<Smo.User>().Select(u => new User(u, _server));

    public string Name => _database.Name;

    public bool Restoring => _database.Status == Smo.DatabaseStatus.Restoring;

    public void RestoreWithRecovery()
    {
      // TODO: Sql injection paranoia. - can we execute normally with a parameterized statement here?
      _database.Parent.Databases["master"].ExecuteNonQuery($"RESTORE DATABASE [{_database.Name}] WITH RECOVERY");
      _database.Refresh();
    }

    /// <summary>
    ///   Kills connections and deletes the database.
    /// </summary>
    public void Drop()
    {
      _database.Parent.KillDatabase(_database.Name);
    }

    /// <summary>
    ///   Queries msdb on the instance for backups of this database.
    /// </summary>
    /// <returns>A list of backups known by msdb</returns>
    public List<BackupMetadata> RecentBackups()
    {
      var backups = new List<BackupMetadata>();

      var query =
        "SELECT s.database_name, m.physical_device_name, s.backup_start_date, s.first_lsn,   s.last_lsn,   " +
        "s.database_backup_lsn,   s.checkpoint_lsn,   s.[type] AS backup_type,   s.server_name, " +
        "s.recovery_model   FROM msdb.dbo.backupset s INNER JOIN msdb.dbo.backupmediafamily m ON s.media_set_id = m.media_set_id WHERE " +
        "s.last_lsn >= (SELECT MAX(last_lsn)  FROM msdb.dbo.backupset WHERE  [type] = 'D' and database_name = @dbName) AND " +
        "s.database_name = @dbName ORDER BY s.backup_start_date DESC, backup_finish_date";

      using(var cmd = _server.SqlConnection.CreateCommand()) {
        cmd.CommandText = query;
        var dbName = cmd.CreateParameter();
        dbName.ParameterName = "dbName";
        dbName.Value = _database.Name;
        cmd.Parameters.Add(dbName);
        using(var reader = cmd.ExecuteReader()) {
          while(reader.Read())
            backups.Add(new BackupMetadata(reader));
        }
      }

      return backups;
    }
  }
}
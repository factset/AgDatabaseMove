namespace AgDatabaseMove.SmoFacade
{
  using System;
  using System.Collections.Generic;
  using System.Data;
  using System.Data.SqlClient;
  using System.IO;
  using System.Linq;
  using Exceptions;
  using Microsoft.SqlServer.Management.Common;
  using Microsoft.SqlServer.Management.Smo;


  /// <summary>
  ///   Adds some better accessors and simplifies some interactions with SMO's Server class.
  /// </summary>
  public class Server : IDisposable
  {
    internal readonly Microsoft.SqlServer.Management.Smo.Server _server;

    public Server(string connectionString)
    {
      SqlConnection = new SqlConnection(connectionString);
      _server = new Microsoft.SqlServer.Management.Smo.Server(new ServerConnection(SqlConnection));
    }

    public Server(SqlConnectionStringBuilder connectionStringBuilder) :
      this(connectionStringBuilder.ConnectionString) { }

    public SqlConnection SqlConnection { get; }

    public IEnumerable<AvailabilityGroup> AvailabilityGroups =>
      _server.AvailabilityGroups.Cast<Microsoft.SqlServer.Management.Smo.AvailabilityGroup>()
        .Select(ag => new AvailabilityGroup(ag));

    public IEnumerable<Database> Databases => _server.Databases.Cast<Microsoft.SqlServer.Management.Smo.Database>()
      .Select(d => new Database(d, this));

    private AvailabilityGroup AvailabilityGroup =>
      AvailabilityGroups.Single(ag => ag.Listeners.Contains(AgName(), StringComparer.InvariantCultureIgnoreCase));

    public string Name => _server.Name;

    public IEnumerable<Login> Logins => _server.Logins.Cast<Microsoft.SqlServer.Management.Smo.Login>()
      .Select(l => new Login(l, this));

    public void Dispose()
    {
      SqlConnection?.Dispose();
    }

    /// <summary>
    ///   Parses the AG name from the connection's DataSource.
    /// </summary>
    /// <returns>The availability group name.</returns>
    private string AgName()
    {
      var dotIndex = SqlConnection?.DataSource?.IndexOf('.');
      if(!dotIndex.HasValue)
        return string.Empty;
      return dotIndex >= 0 ? SqlConnection.DataSource.Remove(dotIndex.Value) : SqlConnection.DataSource;
    }

    /// <summary>
    ///   Queries the database instance for the default file locations.
    /// </summary>
    /// <returns>A DefaultFileLocations object which contains default log and data directories.</returns>
    private DefaultFileLocations DefaultFileLocations()
    {
      var query =
        "SELECT SERVERPROPERTY('InstanceDefaultDataPath') AS InstanceDefaultDataPath, SERVERPROPERTY('InstanceDefaultLogPath') AS InstanceDefaultLogPath";

      using var cmd = SqlConnection.CreateCommand();
      using var reader = cmd.ExecuteReader();
      cmd.CommandText = query;
      if(!reader.Read())
        return null;

      return new DefaultFileLocations
        { Log = (string)reader["InstanceDefaultLogPath"], Data = (string)reader["InstanceDefaultDataPath"] };
    }

    public Database Database(string dbName)
    {
      // The database collection is cached and not invalidated on database creation (with a second server object).
      _server.Databases.Refresh();
      var database = _server.Databases[dbName];
      return database == null ? null : new Database(database, this);
    }

    /// <summary>
    ///   Restores each of the backups. If the default file location is available it will move the files to there.
    /// </summary>
    /// <param name="backupOrder">An ordered list of backups to apply.</param>
    /// <param name="databaseName">Database to restore to.</param>
    /// <param name="fileRelocation">Option for renaming files during the restore.</param>
    public void Restore(IEnumerable<BackupMetadata> backupOrder, string databaseName,
      Func<string, string> fileRelocation = null)
    {
      var restore = new Restore { Database = databaseName, NoRecovery = true };

      foreach(var backup in backupOrder) {
        var device = BackupFileTools.IsUrl(backup.PhysicalDeviceName) ? DeviceType.Url : DeviceType.File;
        var backupDeviceItem = new BackupDeviceItem(backup.PhysicalDeviceName, device);
        restore.Devices.Add(backupDeviceItem);

        var defaultFileLocations = DefaultFileLocations();
        if(defaultFileLocations != null) {
          restore.RelocateFiles.Clear();
          foreach(var file in restore.ReadFileList(_server).AsEnumerable()) {
            var physicalName = (string)file["PhysicalName"];
            var fileName = Path.GetFileName(physicalName) ??
                           throw new InvalidBackupException($"Physical name in backup is incomplete: {physicalName}");

            if(fileRelocation != null)
              fileName = fileRelocation(fileName);

            var path = (string)file["Type"] == "L" ? defaultFileLocations?.Log : defaultFileLocations?.Data;
            path = path ?? Path.GetFullPath(physicalName);

            var newFilePath = Path.Combine(path, fileName);

            restore.RelocateFiles.Add(new RelocateFile((string)file["LogicalName"], newFilePath));
          }
        }

        restore.SqlRestore(_server);
        restore.Devices.Remove(backupDeviceItem);
      }
    }

    /// <summary>
    ///   Generate a log backup, truncating the transaction log, and storing it in the default backup destination.
    ///   TODO: we should support a backup path somehow with configuration
    /// </summary>
    /// <param name="databaseName"></param>
    /// <param name="backupPathTemplate">
    ///   A template string for backup location:
    ///   {0} databaseName
    ///   {1} time
    /// </param>
    public void LogBackup(string databaseName, string backupPathTemplate = null)
    {
      backupPathTemplate = backupPathTemplate ?? DefaultBackupPathTemplate(BackupFileTools.BackupType.Log);
      var backup = new Backup
        { Action = BackupActionType.Log, Database = databaseName, LogTruncation = BackupTruncateLogType.Truncate };
      Backup(backup, backupPathTemplate);
    }

    /// <summary>
    ///   Generate a full backup, not truncating the transaction log, and storing it in the default backup destination.
    ///   TODO: we should support a backup path somehow with configuration
    /// </summary>
    /// <param name="databaseName">name of the database to backup</param>
    /// <param name="backupPathTemplate">
    ///   A template string for backup location:
    ///   {0} databaseName
    ///   {1} time
    /// </param>
    public void FullBackup(string databaseName, string backupPathTemplate = null)
    {
      backupPathTemplate = backupPathTemplate ?? DefaultBackupPathTemplate(BackupFileTools.BackupType.Full);
      var backup = new Backup {
        Action = BackupActionType.Database, Database = databaseName, LogTruncation = BackupTruncateLogType.NoTruncate
      };
      Backup(backup, backupPathTemplate);
    }

    private void Backup(Backup backup, string backupPathTemplate)
    {
      var filePath = string.Format(backupPathTemplate,
                                   backup.Database,
                                   DateTime.Now.ToString("yyyy_MM_dd_hhmmss_fff"));
      var deviceType = BackupFileTools.IsUrl(filePath) ? DeviceType.Url : DeviceType.File;

      var bdi = new BackupDeviceItem(filePath, deviceType);

      backup.Devices.Add(bdi);
      backup.SqlBackup(_server);
    }


    /// <summary>
    ///   Builds a backup path template from the server's default backup directory.
    ///   If this path is not valid on the destination instance the restore process will fail.
    /// </summary>
    private string DefaultBackupPathTemplate(BackupFileTools.BackupType type)
    {
      return _server.BackupDirectory + "\\{0}_backup_{1}." + BackupFileTools.BackupTypeToExtension(type);
    }

    public void EnsureLogins(IEnumerable<LoginProperties> newLogins)
    {
      foreach(var login in newLogins) {
        var matchingLogin =
          Logins.SingleOrDefault(l => l.Name.Equals(login.Name, StringComparison.InvariantCultureIgnoreCase));
        if(matchingLogin == null)
          new Login(login, this);
      }
    }
  }
}
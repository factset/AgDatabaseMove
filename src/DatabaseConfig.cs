namespace AgDatabaseMove
{
  using SmoFacade;


  /// <summary>
  ///   An options class used with AgDatabase.
  /// </summary>
  public class DatabaseConfig
  {
    /// <summary>
    ///   A connection string to connect to an AG listener.
    /// </summary>
    public string ConnectionString { get; set; }

    /// <summary>
    ///   Name of the database to work with.
    /// </summary>
    public string DatabaseName { get; set; }

    /// <summary>
    ///   SQL query text to retrieve the desired backup path location. If none is provided, Server.DefaultBackupPathTemplate is
    ///   used.   See <see cref="Server.DefaultBackupPathTemplate" />
    ///   <example> SELECT '\\my\backup\location\'</example>
    ///   <example> SELECT Backup_path FROM [msdb].[dbo].[_Sys_Backup_config]</example>
    /// </summary>
    public string BackupPathSqlQuery { get; set; }

    /// <summary>
    ///   Server credential used for backup and restore operations. Necessary for SqlServer 2012 and older
    /// </summary>
    public string CredentialName { get; set; }

    public override string ToString()
    {
      return $"{ConnectionString}\n{DatabaseName}\n{BackupPathSqlQuery}\n";
    }
  }
}
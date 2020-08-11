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
    ///   If this isn't supplied we build one from the database default backup path.
    ///   See <see cref="Server.DefaultBackupPathTemplate" />
    /// </summary>
    public string BackupPathTemplate { get; set; }
  }
}
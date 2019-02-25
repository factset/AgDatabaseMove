namespace AgDatabaseMove
{
  using System.Linq;
  using SmoFacade;
  using System;
  using System.Collections.Generic;


  /// <summary>
  ///   Encapsulates the logic for determining the order to apply recent backups.
  /// </summary>
  public class BackupChain : IBackupChain
  {
    private readonly List<BackupMetadata> _orderedBackups;

    private BackupChain(IEnumerable<BackupMetadata> recentBackups)
    {
      var backups = recentBackups.Distinct(new BackupMetadataEqualityComparer()).ToList();
      var lastFullBackup = backups.Where(b => b.BackupType == "D").OrderByDescending(d => d.CheckpointLsn).First();
      _orderedBackups = new List<BackupMetadata> { lastFullBackup };

      var differentialBackup = backups
        .Where(b => b.DatabaseBackupLsn == lastFullBackup.CheckpointLsn && b.BackupType == "I")
        .OrderByDescending(b => b.LastLsn).FirstOrDefault();
      if(differentialBackup != null)
        _orderedBackups.Add(differentialBackup);

      decimal? nextLogLsn = _orderedBackups.Last().LastLsn;
      while(nextLogLsn != null) {
        var log = backups.Where(b => b.BackupType == "L")
          .SingleOrDefault(d => nextLogLsn >= d.FirstLsn && nextLogLsn + 1 < d.LastLsn);
        if(log != null)
          _orderedBackups.Add(log);
        nextLogLsn = log?.LastLsn;
      }
    }

    /// <summary>
    ///   Initializes a backup chain from a database that is part of an AG.
    /// </summary>
    public BackupChain(IAgDatabase agDatabase) : this(agDatabase.RecentBackups()) { }

    /// <summary>
    ///   Initializes a backup chain from a stand alone database that is not part of an AG.
    /// </summary>
    public BackupChain(Database database) : this(database.RecentBackups()) { }

    /// <summary>
    ///   Backups ordered to have a full restore chain.
    /// </summary>
    public IEnumerable<BackupMetadata> RestoreOrder => _orderedBackups;

    /// <summary>
    ///   This assumes your access to the backup files is the same as they will be on the sql server(s) which run the restore
    ///   operations.
    ///   This should be an extra safety check run before restoring with an overwrite flag so you don't get stuck mid backup.
    ///   Perhaps I should restore with file list only each of these from the server instead of just checking the file exists?
    /// </summary>
    private void ValidateBackupFiles()
    {
      // TODO: implement backup validation.
      throw new NotImplementedException();
    }
  }
}
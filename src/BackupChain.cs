namespace AgDatabaseMove
{
  using System.Linq;
  using SmoFacade;
  using System;
  using System.Collections.Generic;
  using System.IO;
  using Exceptions;


  /// <summary>
  ///   Encapsulates the logic for determining the order to apply recent backups.
  /// </summary>
  public class BackupChain : IBackupChain
  {
    private readonly List<BackupMetadata> _orderedBackups;

    private BackupChain(IEnumerable<BackupMetadata> recentBackups)
    {
      var backups = recentBackups
        .Distinct(new BackupMetadataEqualityComparer())
        .Where(b => IsValidFilePath(b.PhysicalDeviceName)) // A third party application caused invalid path strings to be inserted into backupmediafamily
        .ToList();

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

      if(_orderedBackups.Last().LastLsn != backups.Max(b => b.LastLsn))
        throw new BackupChainException("Backup chain does not include the latest log backup.");
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
    ///   This should be an extra safety check run before restoring with an overwrite flag so you don't get stuck mid restore.
    ///   Restore with file list only on each of these from the server instead of just checking the file exists.
    /// </summary>
    private void ValidateBackupFiles()
    {
      // TODO: implement backup validation.
      throw new NotImplementedException();
    }

    private bool IsValidFilePath(string path)
    {
      // A quick check before leaning on exceptions
      if(Path.GetInvalidPathChars().Any(path.Contains))
        return false;

      try {
        // This will throw an argument exception if the path is invalid
        Path.GetFullPath(path);
        // A relative path won't help us much if the destination is another server. It needs to be rooted.
        return Path.IsPathRooted(path);
      }
      catch(ArgumentException) {
        return false;
      }
    }
  }
}
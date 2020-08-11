namespace AgDatabaseMove
{
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using Exceptions;
  using SmoFacade;


  public interface IBackupChain
  {
    IEnumerable<BackupMetadata> RestoreOrder { get; }
  }

  /// <summary>
  ///   Encapsulates the logic for determining the order to apply recent backups.
  /// </summary>
  public class BackupChain : IBackupChain
  {
    private readonly IList<BackupMetadata> _orderedBackups;

    private BackupChain(IList<BackupMetadata> recentBackups)
    {
      var backups = recentBackups.Distinct(new BackupMetadataEqualityComparer())
        .Where(b => IsValidFilePath(b)) // A third party application caused invalid path strings to be inserted into backupmediafamily
        .ToList();

      var lastFullBackup = LastFullBackup(recentBackups);
      _orderedBackups = new List<BackupMetadata> { lastFullBackup };

      var differentialBackup = NextDifferentialBackup(backups, lastFullBackup);

      if(differentialBackup != null)
        _orderedBackups.Add(differentialBackup);

      decimal? nextLogLsn = _orderedBackups.Last().LastLsn;
      while(nextLogLsn != null) {
        var logBackup = NextLogBackup(backups, nextLogLsn);

        if(logBackup != null)
          _orderedBackups.Add(logBackup);
        nextLogLsn = logBackup?.LastLsn;
      }

      if(_orderedBackups.Last().LastLsn != backups.Max(b => b.LastLsn))
        throw new BackupChainException("Backup chain does not include the latest log backup");
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

    private BackupMetadata LastFullBackup(IList<BackupMetadata> backups)
    {
      return backups.Where(b => b.BackupType == "D").OrderByDescending(d => d.CheckpointLsn).First();
    }

    private BackupMetadata NextDifferentialBackup(IList<BackupMetadata> backups, BackupMetadata lastFullBackup)
    {
      return backups.Where(b => b.BackupType == "I" && b.DatabaseBackupLsn == lastFullBackup.CheckpointLsn)
        .OrderByDescending(b => b.LastLsn).FirstOrDefault();
    }

    private BackupMetadata NextLogBackup(IList<BackupMetadata> backups, decimal? nextLogLsn)
    {
      return backups.Where(b => b.BackupType == "L")
        .SingleOrDefault(d => nextLogLsn >= d.FirstLsn && nextLogLsn + 1 < d.LastLsn);
    }

    private bool IsValidFilePath(BackupMetadata meta)
    {
      var path = meta.PhysicalDeviceName;
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
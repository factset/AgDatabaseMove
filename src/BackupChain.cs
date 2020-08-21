namespace AgDatabaseMove
{
  using System;
  using System.Collections.Generic;
  using System.IO;
  using System.Linq;
  using SmoFacade;


  public interface IBackupChain
  {
    IEnumerable<BackupMetadata> OrderedBackups { get; }
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

      var mostRecentFullBackup = MostRecentFullBackup(recentBackups);
      _orderedBackups = new List<BackupMetadata> { mostRecentFullBackup };

      var differentialBackup = MostRecentDifferentialBackup(backups, mostRecentFullBackup);
      if(differentialBackup != null)
        _orderedBackups.Add(differentialBackup);

      var mostRecentBackup = _orderedBackups.Last();
      while(mostRecentBackup != null) {
        mostRecentBackup = NextLogBackup(backups, mostRecentBackup);

        if(mostRecentBackup != null)
          _orderedBackups.Add(mostRecentBackup);
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
    public IEnumerable<BackupMetadata> OrderedBackups => _orderedBackups;

    private BackupMetadata MostRecentFullBackup(IList<BackupMetadata> backups)
    {
      return backups.Where(b => b.BackupType == BackupFileTools.BackupType.Full).OrderByDescending(d => d.CheckpointLsn)
        .First();
    }

    private BackupMetadata MostRecentDifferentialBackup(IList<BackupMetadata> backups, BackupMetadata lastFullBackup)
    {
      return backups.Where(b => b.BackupType == BackupFileTools.BackupType.Diff &&
                                b.DatabaseBackupLsn == lastFullBackup.CheckpointLsn)
        .OrderByDescending(b => b.LastLsn).FirstOrDefault();
    }

    private BackupMetadata NextLogBackup(IList<BackupMetadata> backups, BackupMetadata prevBackup)
    {
      return backups.Where(b => b.BackupType == BackupFileTools.BackupType.Log)
        .SingleOrDefault(d => prevBackup.LastLsn >= d.FirstLsn && prevBackup.LastLsn + 1 < d.LastLsn);
    }

    private bool IsValidFilePath(BackupMetadata meta)
    {
      if(BackupFileTools.IsUrl(meta.PhysicalDeviceName))
        return true;

      // A quick check before leaning on exceptions
      if(Path.GetInvalidPathChars().Any(meta.PhysicalDeviceName.Contains))
        return false;

      try {
        // This will throw an argument exception if the path is invalid
        Path.GetFullPath(meta.PhysicalDeviceName);
        // A relative path won't help us much if the destination is another server. It needs to be rooted.
        return Path.IsPathRooted(meta.PhysicalDeviceName);
      }
      catch(Exception) {
        return false;
      }
    }
  }
}
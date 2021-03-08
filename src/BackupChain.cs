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
    IEnumerable<BackupMetadata> OrderedBackups { get; }
  }

  /// <summary>
  ///   Encapsulates the logic for determining the order to apply recent backups.
  /// </summary>
  public class BackupChain : IBackupChain
  {
    private readonly IList<BackupMetadata> _orderedBackups;

    // This also handles any striped backups
    private BackupChain(IList<BackupMetadata> recentBackups)
    {
      if (recentBackups == null || recentBackups.Count == 0) {
        throw new BackupChainException("There are no recent backups to form a chain");
      }

      var backups = recentBackups.Distinct(new BackupMetadataEqualityComparer())
        .Where(IsValidFilePath) // A third party application caused invalid path strings to be inserted into backupmediafamily
        .ToList();

      var orderedBackups = MostRecentFullBackup(backups).ToList();
      orderedBackups.AddRange(MostRecentDiffBackup(backups, orderedBackups.First()));

      var prevBackup = orderedBackups.Last();
      IEnumerable<BackupMetadata> nextLogBackups;
      while((nextLogBackups = NextLogBackup(backups, prevBackup)).Any()) {
        orderedBackups.AddRange(nextLogBackups);
        prevBackup = orderedBackups.Last();
      }

      _orderedBackups = orderedBackups;
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

    private static IEnumerable<BackupMetadata> MostRecentFullBackup(IEnumerable<BackupMetadata> backups)
    {
      var fullBackupsOrdered = backups
        .Where(b => b.BackupType == BackupFileTools.BackupType.Full)
        .OrderByDescending(d => d.CheckpointLsn).ToList();
      
      if(!fullBackupsOrdered.Any()) {
        throw new BackupChainException("Could not find any full backups");
      }

      var targetCheckpointLsn = fullBackupsOrdered.First().CheckpointLsn;
      // get all the stripes of this backup
      return fullBackupsOrdered.Where(fullBackup => fullBackup.CheckpointLsn == targetCheckpointLsn); 
    }

    private static IEnumerable<BackupMetadata> MostRecentDiffBackup(IEnumerable<BackupMetadata> backups, BackupMetadata lastFullBackup)
    {
      var diffBackupsOrdered = backups
        .Where(b => b.BackupType == BackupFileTools.BackupType.Diff &&
                    b.DatabaseBackupLsn == lastFullBackup.CheckpointLsn)
        .OrderByDescending(b => b.LastLsn).ToList();

      if (!diffBackupsOrdered.Any()) {
        return new List<BackupMetadata>();
      }
      var targetLastLsn = diffBackupsOrdered.First().LastLsn;
      // get all the stripes of this backup
      return diffBackupsOrdered.Where(diffBackup => diffBackup.LastLsn == targetLastLsn); 
    }

    private static IEnumerable<BackupMetadata> NextLogBackup(IEnumerable<BackupMetadata> backups, BackupMetadata prevBackup)
    {
      // also gets all the stripes of the next backup
      return backups.Where(b => b.BackupType == BackupFileTools.BackupType.Log && 
                                prevBackup.LastLsn >= b.FirstLsn && prevBackup.LastLsn + 1 < b.LastLsn);
    }

    private static bool IsValidFilePath(BackupMetadata meta)
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
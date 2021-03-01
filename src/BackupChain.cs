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

    /// <summary>
    /// Chain needs to be 1 full backup, 1 diff and then the logs in the order they were taken
    /// Sometimes backups can be striped (i.e. split into multiple files) - so we need to handle these cases too
    /// https://www.sqlservercentral.com/articles/getting-a-list-of-the-striped-backup-files
    /// </summary>
    /// <param name="recentBackups"></param>
    private BackupChain(IList<BackupMetadata> recentBackups)
    {
      var backups = recentBackups.Distinct(new BackupMetadataEqualityComparer())
        .Where(IsValidFilePath) // A third party application caused invalid path strings to be inserted into backupmediafamily
        .ToList();

      var mostRecentFullBackups = MostRecentFullBackups(backups).ToList();
      
      var differentialBackups = MostRecentDifferentialBackups(backups, mostRecentFullBackups.First());
      // differentialBackups can be null
      
      var orderedBackups = mostRecentFullBackups.Concat(differentialBackups).ToList();

      var prevBackup = orderedBackups.Last();
      IEnumerable<BackupMetadata> nextLogBackups;
      while((nextLogBackups = NextLogBackups(backups, prevBackup)).Any()) {
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

    private static IEnumerable<BackupMetadata> MostRecentFullBackups(IEnumerable<BackupMetadata> backups)
    {
      var fullBackupsOrdered = backups
        .Where(b => b.BackupType == BackupFileTools.BackupType.Full)
        .OrderByDescending(d => d.CheckpointLsn).ToList();
      if(!fullBackupsOrdered.Any()) {
        throw new BackupChainException("Could not find any full backups");
      }

      var targetCheckpointLsn = fullBackupsOrdered.First().CheckpointLsn;
      // get all the stripes of the most recent full backup (i.e. has the same CheckpointLsn)
      return fullBackupsOrdered.Where(fullBackup => fullBackup.CheckpointLsn == targetCheckpointLsn); 
    }

    private static IEnumerable<BackupMetadata> MostRecentDifferentialBackups(IEnumerable<BackupMetadata> backups, BackupMetadata lastFullBackup)
    {
      var diffBackupsOrdered = backups
        .Where(b => b.BackupType == BackupFileTools.BackupType.Diff &&
                    b.DatabaseBackupLsn == lastFullBackup.CheckpointLsn)
        .OrderByDescending(b => b.LastLsn).ToList();

      var targetLastLsn = diffBackupsOrdered.First().LastLsn;
      // get all the stripes of the most recent diff backup (i.e. has the same LastLsn)
      return diffBackupsOrdered.Where(diffBackup => diffBackup.LastLsn == targetLastLsn); 
    }

    private static IEnumerable<BackupMetadata> NextLogBackups(IEnumerable<BackupMetadata> backups, BackupMetadata prevBackup)
    {
      // get all the stripes of the next log backup
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
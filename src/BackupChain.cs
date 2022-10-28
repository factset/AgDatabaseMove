namespace AgDatabaseMove
{
  using System.Collections.Generic;
  using System.Linq;
  using Exceptions;
  using SmoFacade;


  public interface IBackupChain
  {
    IEnumerable<StripedBackupSet> OrderedBackups { get; }
  }

  /// <summary>
  ///   Encapsulates the logic for determining the order to apply recent backups.
  /// </summary>
  public class BackupChain : IBackupChain
  {
    private readonly IList<StripedBackupSet> _orderedBackups;

    // This also handles any striped backups
    private BackupChain(IList<BackupMetadata> recentBackups)
    {
      if(recentBackups == null || recentBackups.Count == 0)
        throw new BackupChainException("There are no recent backups to form a chain");

      var backups = recentBackups
                    .Distinct(BackupMetadataEqualityComparer.Instance)
                    .Where(IsValidFilePath); // A third party application caused invalid path strings to be inserted into backupmediafamily
      var stripedBackups = StripedBackupSet.GetStripedBackupSetChain(backups);

      var orderedBackups = new List<StripedBackupSet> { MostRecentFullBackup(stripedBackups) };
      var diff = MostRecentDiffBackup(stripedBackups, orderedBackups.First());
      if (diff != null) { orderedBackups.Add(diff); }

      var nextLog = FirstLogInChain(stripedBackups, orderedBackups.Last());
      if (nextLog != null) { orderedBackups.Add(nextLog); }

      var prevBackup = orderedBackups.Last();
      while((nextLog = NextLogBackup(stripedBackups, prevBackup)) != null) {
        orderedBackups.Add(nextLog);
        prevBackup = nextLog;
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
    public BackupChain(Database database) : this(database.MostRecentBackupChain()) { }

    /// <summary>
    ///   Backups ordered to have a full restore chain.
    /// </summary>
    public IEnumerable<StripedBackupSet> OrderedBackups => _orderedBackups;

    private static StripedBackupSet MostRecentFullBackup(IEnumerable<StripedBackupSet> stripedBackups)
    {
      var fullBackupsOrdered = stripedBackups
        .Where(b => b.BackupType == BackupFileTools.BackupType.Full)
        .OrderByDescending(d => d.CheckpointLsn).ToList();

      if(!fullBackupsOrdered.Any())
        throw new BackupChainException("Could not find any full backups");

      return fullBackupsOrdered.First();
    }

    private static StripedBackupSet MostRecentDiffBackup(IEnumerable<StripedBackupSet> stripedBackups,
      BackupMetadata lastFullBackup)
    {
      return stripedBackups.OrderByDescending(b => b.LastLsn)
                    .FirstOrDefault(b => b.BackupType == BackupFileTools.BackupType.Diff 
                                        && b.DatabaseBackupLsn == lastFullBackup.CheckpointLsn);
    }

    private static StripedBackupSet NextLogBackup(IEnumerable<StripedBackupSet> stripedBackups,
      BackupMetadata prevLog)
    {
      return stripedBackups.SingleOrDefault(b => b.BackupType == BackupFileTools.BackupType.Log &&
                                          prevLog.LastLsn == b.FirstLsn);
    }

    private static StripedBackupSet FirstLogInChain(IEnumerable<StripedBackupSet> stripedBackups, 
     BackupMetadata lastBackup)
    {
      return stripedBackups.OrderByDescending(b => b.LastLsn)
        .FirstOrDefault(b => b.BackupType == BackupFileTools.BackupType.Log && 
                             lastBackup.LastLsn >= b.FirstLsn &&
                             lastBackup.LastLsn <= b.LastLsn);
    }

    private static bool IsValidFilePath(BackupMetadata meta)
    {
      var path = meta.PhysicalDeviceName;
      return BackupFileTools.IsValidFileUrl(path) || BackupFileTools.IsValidFilePath(path);
    }
  }
}

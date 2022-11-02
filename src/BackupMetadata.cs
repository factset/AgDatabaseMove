namespace AgDatabaseMove
{
  using System;
  using System.Collections.Generic;
  using System.Linq;
  using SmoFacade;

  public class StripedBackupEqualityComparer : IEqualityComparer<SingleBackup>
  {
    private static readonly Lazy<StripedBackupEqualityComparer> s_instance = new Lazy<StripedBackupEqualityComparer>(() => new StripedBackupEqualityComparer());
    private StripedBackupEqualityComparer() { }
    public static StripedBackupEqualityComparer Instance => s_instance.Value;

    public bool Equals(SingleBackup x, SingleBackup y)
    {
      return x.LastLsn == y.LastLsn &&
             x.FirstLsn == y.FirstLsn &&
             x.BackupType == y.BackupType &&
             x.DatabaseName == y.DatabaseName &&
             x.CheckpointLsn == y.CheckpointLsn &&
             x.DatabaseBackupLsn == y.DatabaseBackupLsn;
    }

    public int GetHashCode(SingleBackup obj)
    {
      var hashCode = -1277603921;
      hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(obj.DatabaseName);
      hashCode = hashCode * -1521134295 +
                 EqualityComparer<BackupFileTools.BackupType>.Default.GetHashCode(obj.BackupType);
      hashCode = hashCode * -1521134295 + obj.FirstLsn.GetHashCode();
      hashCode = hashCode * -1521134295 + obj.LastLsn.GetHashCode();
      hashCode = hashCode * -1521134295 + obj.CheckpointLsn.GetHashCode();
      hashCode = hashCode * -1521134295 + obj.DatabaseBackupLsn.GetHashCode();
      return hashCode;
    }

  }

  /// <summary>
  /// Two BackupMetadatas are the same, if they are like striped backups but also have the same `PhysicalDeviceName`
  /// </summary>
  public class BackupMetadataEqualityComparer : IEqualityComparer<SingleBackup>
  {
    private static readonly StripedBackupEqualityComparer _stripedBackupEqualityComparer = StripedBackupEqualityComparer.Instance;

    private static readonly Lazy<BackupMetadataEqualityComparer> s_instance = new Lazy<BackupMetadataEqualityComparer>(() => new BackupMetadataEqualityComparer());
    private BackupMetadataEqualityComparer() { }
    public static BackupMetadataEqualityComparer Instance => s_instance.Value;

    public bool Equals(SingleBackup x, SingleBackup y)
    {
      return _stripedBackupEqualityComparer.Equals(x, y)
        && x.PhysicalDeviceName == y.PhysicalDeviceName;
    }

    public int GetHashCode(SingleBackup obj)
    {
      var hashCode = _stripedBackupEqualityComparer.GetHashCode(obj);
      hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(obj.PhysicalDeviceName);
      return hashCode;
    }
  }

  /// <summary>
  ///   Metadata about backups from msdb.dbo.backupset and msdb.dbo.backupmediafamily
  /// </summary>
  public abstract class BackupMetadata : ICloneable
  {
    public decimal CheckpointLsn { get; set; }
    public decimal DatabaseBackupLsn { get; set; }
    public string DatabaseName { get; set; }
    public decimal FirstLsn { get; set; }
    public decimal LastLsn { get; set; }
    public string ServerName { get; set; }
    public DateTime StartTime { get; set; }

    /// <summary>
    ///   Type of backup
    ///   D = Database, I = Differential database, L = Log
    ///   https://docs.microsoft.com/en-us/sql/relational-databases/system-tables/backupset-transact-sql?view=sql-server-2017
    /// </summary>
    public BackupFileTools.BackupType BackupType { get; set; }

    internal BackupMetadata() { }

    internal BackupMetadata(BackupMetadata backup)
    {
      CheckpointLsn = backup.CheckpointLsn;
      DatabaseBackupLsn = backup.DatabaseBackupLsn;
      DatabaseName = backup.DatabaseName;
      FirstLsn = backup.FirstLsn;
      LastLsn = backup.LastLsn;
      ServerName = backup.ServerName;
      StartTime = backup.StartTime;
      BackupType = backup.BackupType;
    }

    // used during testing
    public object Clone()
    {
      return MemberwiseClone();
    }
 }

  public class SingleBackup : BackupMetadata
  {
    public string PhysicalDeviceName { get; set; }
  }

  public class StripedBackup : BackupMetadata
  {
    public IEnumerable<SingleBackup> StripedBackups { get; }

    private StripedBackup(IEnumerable<SingleBackup> stripedBackups) : base(stripedBackups.First())
    {
      StripedBackups = stripedBackups;
    }

    internal static IEnumerable<StripedBackup> GetStripedBackupSetChain(IEnumerable<SingleBackup> backups)
    {
      var chain = backups
        .GroupBy(b => b, StripedBackupEqualityComparer.Instance)
        .Select(group => new StripedBackup(group));
      return chain;
    }
  }
}
namespace AgDatabaseMove
{
  using System;
  using System.Collections.Generic;
  using SmoFacade;


  public class BackupEqualityComparer : IEqualityComparer<BackupMetadata>
  {
    public bool Equals(BackupMetadata x, BackupMetadata y)
    {
      return x.LastLsn == y.LastLsn &&
             x.FirstLsn == y.FirstLsn &&
             x.BackupType == y.BackupType &&
             x.DatabaseName == y.DatabaseName &&
             x.CheckpointLsn == y.CheckpointLsn &&
             x.DatabaseBackupLsn == x.DatabaseBackupLsn;
    }

    public int GetHashCode(BackupMetadata obj)
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

  public class BackupMetadataEqualityComparer : IEqualityComparer<BackupMetadata>
  {
    public bool Equals(BackupMetadata x, BackupMetadata y)
    {
      return new BackupEqualityComparer()
        .Equals(x, y)
        && x.PhysicalDeviceName == y.PhysicalDeviceName;
    }

    public int GetHashCode(BackupMetadata obj)
    {
      var hashCode = new BackupEqualityComparer().GetHashCode(obj);
      hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(obj.PhysicalDeviceName);
      return hashCode;
    }
  }

  /// <summary>
  ///   Metadata about backups from msdb.dbo.backupset and msdb.dbo.backupmediafamily
  /// </summary>
  public class BackupMetadata : ICloneable
  {
    public decimal CheckpointLsn { get; set; }
    public decimal DatabaseBackupLsn { get; set; }
    public string DatabaseName { get; set; }
    public decimal FirstLsn { get; set; }
    public decimal LastLsn { get; set; }
    public string PhysicalDeviceName { get; set; }
    public string ServerName { get; set; }
    public DateTime StartTime { get; set; }

    /// <summary>
    ///   Type of backup
    ///   D = Database, I = Differential database, L = Log
    ///   https://docs.microsoft.com/en-us/sql/relational-databases/system-tables/backupset-transact-sql?view=sql-server-2017
    /// </summary>
    public BackupFileTools.BackupType BackupType { get; set; }

    // used during testing
    public object Clone()
    {
      return MemberwiseClone();
    }
  }
}
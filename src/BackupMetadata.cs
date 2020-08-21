namespace AgDatabaseMove
{
  using System;
  using System.Collections.Generic;
  using SmoFacade;


  /// <summary>
  ///   Occasionally we wind up with the same entry for a backup on multiple instance's msdb.
  ///   For now we'll consider these backups to be equal despite their file location,
  ///   but perhaps there's value in being able to look for the file in multiple locations.
  /// </summary>
  public class BackupMetadataEqualityComparer : IEqualityComparer<BackupMetadata>
  {
    public bool Equals(BackupMetadata x, BackupMetadata y)
    {
      return x.LastLsn == y.LastLsn &&
             x.FirstLsn == y.FirstLsn &&
             x.BackupType == y.BackupType &&
             x.DatabaseName == y.DatabaseName;
    }

    public int GetHashCode(BackupMetadata obj)
    {
      var hashCode = -1277603921;
      hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(obj.DatabaseName);
      hashCode = hashCode * -1521134295 +
                 EqualityComparer<BackupFileTools.BackupType>.Default.GetHashCode(obj.BackupType);
      hashCode = hashCode * -1521134295 + obj.FirstLsn.GetHashCode();
      hashCode = hashCode * -1521134295 + obj.LastLsn.GetHashCode();
      return hashCode;
    }
  }

  /// <summary>
  ///   Metadata about backups from msdb.dbo.backupset and msdb.dbo.backupmediafamily
  /// </summary>
  public class BackupMetadata
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
  }
}
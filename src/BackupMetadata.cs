namespace AgDatabaseMove
{
  using System;
  using System.Collections.Generic;
  using System.Data.SqlClient;


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
      hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(obj.BackupType);
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
    public BackupMetadata() { }

    internal BackupMetadata(SqlDataReader dataReader)
    {
      CheckpointLsn = (decimal)dataReader["checkpoint_lsn"];
      DatabaseBackupLsn = (decimal)dataReader["database_backup_lsn"];
      DatabaseName = (string)dataReader["database_name"];
      FirstLsn = (decimal)dataReader["first_lsn"];
      LastLsn = (decimal)dataReader["last_lsn"];
      PhysicalDeviceName = (string)dataReader["physical_device_name"];
      ServerName = (string)dataReader["server_name"];
      StartTime = (DateTime)dataReader["backup_start_date"];
      BackupType = (string)dataReader["backup_type"];
    }

    public decimal CheckpointLsn { get; set; }
    public decimal DatabaseBackupLsn { get; set; }
    public string DatabaseName { get; set; }
    public decimal FirstLsn { get; set; }
    public decimal LastLsn { get; set; }
    public string PhysicalDeviceName { get; set; }
    public string ServerName { get; set; }
    public DateTime StartTime { get; set; }
    public string BackupType { get; set; }
  }
}
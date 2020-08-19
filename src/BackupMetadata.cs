namespace AgDatabaseMove
{
  using System;
  using System.Collections.Generic;
  using System.Text.RegularExpressions;
  using Microsoft.SqlServer.Management.Smo;


  public static class FileTools
  {
    public enum BackupType
    {
      Full, // bak  / D
      Diff, // diff / I
      Log // trn  / L
    }

    public static bool IsUrl(string path)
    {
      return Regex.IsMatch(path, @"(https:\/)(\/[a-z0-9\.\-]+)+\.(bak|trn|full|diff)");
    }

    public static string BackupTypeToExtension(BackupType type)
    {
      switch(type) {
        case BackupType.Full:
          return "bak";
        case BackupType.Diff:
          return "diff";
        case BackupType.Log:
          return "trn";
        default:
          throw new ArgumentException("Invalid enum type");
      }
    }

    public static BackupType BackupTypeAbbrevToType(string type)
    {
      switch(type) {
        case "D":
          return BackupType.Full;
        case "I":
          return BackupType.Diff;
        case "L":
          return BackupType.Log;
        default:
          throw new ArgumentException("Invalid backup type");
      }
    }
  }


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
      hashCode = hashCode * -1521134295 + EqualityComparer<FileTools.BackupType>.Default.GetHashCode(obj.BackupType);
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
    public FileTools.BackupType BackupType { get; set; }

    public DeviceType Device => FileTools.IsUrl(PhysicalDeviceName) ? DeviceType.Url : DeviceType.File;
  }
}
namespace AgDatabaseMove.SmoFacade
{
  using System;
  using System.Text.RegularExpressions;


  public static class BackupFileTools
  {
    public enum BackupType
    {
      Full, // bak  / D
      Diff, // diff / I
      Log // trn  / L
    }

    public static bool IsUrl(string path)
    {
      return Regex.IsMatch(path, @"(http(|s):\/)(\/[a-z0-9\.\-]+)+\.([a-zA-Z]+)$");
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
}
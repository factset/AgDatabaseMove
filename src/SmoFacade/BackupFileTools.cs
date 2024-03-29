﻿namespace AgDatabaseMove.SmoFacade
{
  using System;
  using System.IO;
  using System.Linq;


  public static class BackupFileTools
  {
    public enum BackupType
    {
      Full, // bak  / D
      Diff, // diff / I
      Log // trn  / L
    }

    public static bool IsValidFileUrl(string path)
    {
      return Uri.TryCreate(path, UriKind.Absolute, out var uriResult)
             && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps || uriResult.IsUnc)
             && Path.HasExtension(path);
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

    public static bool IsValidFilePath(string path)
    {
      // A quick check before leaning on exceptions
      if(Path.GetInvalidPathChars().Any(path.Contains)) return false;

      try {
        // This will throw an argument exception if the path is invalid
        Path.GetFullPath(path);
        // A relative path won't help us much if the destination is another server. It needs to be rooted.
        return Path.IsPathRooted(path) && Path.HasExtension(path);
      }
      catch(Exception) {
        return false;
      }
    }

    public static string CombinePaths(string path1, string path2)
    {
      if (string.IsNullOrEmpty(path1)) { return path2; }
      if (string.IsNullOrEmpty(path2)) { return path1; }

      // assumes path1 contains desired separators
      var separator = path1.Contains(@"\") ? @"\" : "/";
      var path = path1.EndsWith(separator) ? path1.Substring(0, path1.Length - 1) : path1;
      var file = path2.StartsWith(separator) ? path2.Substring(1) : path2;
      return $"{path}{separator}{file}";
    }
  }
}
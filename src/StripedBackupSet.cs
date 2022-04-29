using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace AgDatabaseMove
{

  public class StripedBackupSet
  {
    public List<BackupMetadata> StripedBackups { get; private set; }

    public StripedBackupSet(List<BackupMetadata> backups, BackupMetadata backupToMatch)
    {
      var stripes = backups.Where(backup => IsStriped(backup, backupToMatch));
      StripedBackups = new List<BackupMetadata>(stripes);
    }

    private bool IsStriped(BackupMetadata backup, BackupMetadata backupToMatch)
    {
      return new BackupEqualityComparer().Equals(backup, backupToMatch);
    }

    public static List<StripedBackupSet> GetStripedBackupSetChain(List<BackupMetadata> backups)
    {
      List<StripedBackupSet> chain = new List<StripedBackupSet>(); 
      while(backups.Count > 0)
      {
        var backup = backups.First();
        var stripedBackupSet = new StripedBackupSet(backups, backup);
        chain.Add(stripedBackupSet);
        backups = backups.Except(stripedBackupSet.StripedBackups, new BackupEqualityComparer()).ToList();
      }
      return chain;
    }
  }
}

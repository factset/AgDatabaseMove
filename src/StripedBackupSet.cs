using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace AgDatabaseMove
{

  public class StripedBackupSet
  {
    public IEnumerable<BackupMetadata> StripedBackups { get; private set; }

    public StripedBackupSet(IEnumerable<BackupMetadata> backups, BackupMetadata backupToMatch)
    {
      var stripes = backups.Where(backup => AreTwoBackupsStriped(backup, backupToMatch));
      StripedBackups = new List<BackupMetadata>(stripes);
    }

    public static bool AreTwoBackupsStriped(BackupMetadata backup, BackupMetadata backupToMatch)
    {
      return new BackupMetadataEqualityComparer().EqualsExceptForPhysicalDeviceName(backup, backupToMatch);
    }

    public static IEnumerable<StripedBackupSet> GetStripedBackupSetChain(IEnumerable<BackupMetadata> backups)
    {
      List<StripedBackupSet> chain = new List<StripedBackupSet>(); 
      while(backups.Count() > 0)
      {
        var backup = backups.First();
        var stripedBackupSet = new StripedBackupSet(backups, backup);
        chain.Add(stripedBackupSet);
        backups = backups.Except(stripedBackupSet.StripedBackups, new BackupMetadataEqualityComparer());
      }
      return chain;
    }
  }
}

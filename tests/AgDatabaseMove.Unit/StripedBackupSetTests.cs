using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit;

namespace AgDatabaseMove.Unit
{
  public class StripedBackupSetTests
  {

    private static readonly StripedBackupEqualityComparer stripedBackupComparer = StripedBackupEqualityComparer.Instance;
    private static readonly BackupMetadataEqualityComparer backupComparer = BackupMetadataEqualityComparer.Instance;

    [Fact]
    public void CombinesStripedBackups()
    {
      var backupChain = BackupOrder.GetBackupListWithStripes();

      var stripedBackupSetChain = StripedBackup.GetStripedBackupChain(backupChain);

      Assert.Equal(backupChain.Distinct(stripedBackupComparer).Count(), stripedBackupSetChain.Count());
    }

    [Fact]
    public void DoesntCombineNonStripedBackups()
    {
      var backupChain = BackupOrder.GetBackupList();
      var stripedBackupSetChain = StripedBackup.GetStripedBackupChain(backupChain);

      Assert.Equal(backupChain.Count, stripedBackupSetChain.Count());
    }

    [Fact]
    public void StripedBackupsAreEqualExceptForPhysicalDeviceName()
    {
      var backupChain = BackupOrder.GetBackupListWithStripes();
    

      var stripedBackupSetChain = StripedBackup.GetStripedBackupChain(backupChain);

      foreach (var stripedBackupSet in stripedBackupSetChain)
      {
        var stripes = stripedBackupSet.StripedBackups.ToList();
        var backup = stripes.First();
        for (int i = 1; i < stripes.Count; i++)
        {
          var otherBackup = stripes[i];
          
          Assert.Equal(backup, otherBackup, stripedBackupComparer);
          Assert.NotEqual(backup, otherBackup, backupComparer);
        }
      }
    }
  }
}

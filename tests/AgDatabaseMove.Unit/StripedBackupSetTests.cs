using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Xunit;

namespace AgDatabaseMove.Unit
{
  public class StripedBackupSetTests
  {

    [Fact]
    public void CombinesStripedBackups()
    {
      var backupChain = BackupOrder.GetBackupListWithStripes();
      var stripedBackupSetChain = StripedBackupSet.GetStripedBackupSetChain(backupChain);

      Assert.Equal(backupChain.Count/2, stripedBackupSetChain.Count());
    }

    [Fact]
    public void DoesntCombineNonStripedBackups()
    {
      var backupChain = BackupOrder.GetBackupList();
      var stripedBackupSetChain = StripedBackupSet.GetStripedBackupSetChain(backupChain);

      Assert.Equal(backupChain.Count, stripedBackupSetChain.Count());
    }

    [Fact]
    public void StripedBackupsAreEqualExceptForPhysicalDeviceName()
    {
      var backupChain = BackupOrder.GetBackupListWithStripes();
      var stripedBackupSetChain = StripedBackupSet.GetStripedBackupSetChain(backupChain);

      foreach (var stripedBackupSet in stripedBackupSetChain)
      {
        var stripes = stripedBackupSet.StripedBackups.ToList();
        var backup = stripes.First();
        for (int i = 1; i < stripes.Count; i++)
        {
          var otherBackup = stripes[i];
          
          Assert.True(new BackupMetadataEqualityComparer().EqualsExceptForPhysicalDeviceName(backup, otherBackup));
          Assert.NotEqual(backup, otherBackup, new BackupMetadataEqualityComparer());
        }
      }
    }
  }
}

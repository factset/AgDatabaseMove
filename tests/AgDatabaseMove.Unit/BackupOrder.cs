namespace AgDatabaseMove.Unit
{
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Linq;
  using Moq;
  using SmoFacade;
  using Xunit;


  public class BackupOrder
  {
    private readonly List<BackupMetadata> _listBackups;

    public BackupOrder()
    {
      _listBackups = ListBackups();
    }

    private static List<BackupMetadata> ListBackups()
    {
      return new List<BackupMetadata> {
        new BackupMetadata {
          BackupType = BackupFileTools.BackupType.Log,
          DatabaseBackupLsn = 126000000943800037,
          CheckpointLsn = 126000000953600034,
          FirstLsn = 126000000955500001,
          LastLsn = 126000000955800001,
          DatabaseName = "TestDb",
          ServerName = "ServerB",
          PhysicalDeviceName = @"\\DFS\BACKUP\ServerB\testDb\Testdb_backup_2018_10_29_030006_660.trn",
          StartTime = DateTime.Parse("2018-10-29 03:00:06.000")
        },
        new BackupMetadata {
          BackupType = BackupFileTools.BackupType.Full,
          DatabaseBackupLsn = 126000000882000037,
          CheckpointLsn = 126000000943800037,
          FirstLsn = 126000000936100001,
          LastLsn = 126000000945500001,
          DatabaseName = "TestDb",
          ServerName = "ServerA",
          PhysicalDeviceName = @"\\DFS\BACKUP\ServerA\testDb\Testdb_backup_2018_10_28_000227_200.full",
          StartTime = DateTime.Parse("2018-10-28 00:02:28.000")
        },
        new BackupMetadata {
          BackupType = BackupFileTools.BackupType.Diff,
          DatabaseBackupLsn = 126000000943800037,
          CheckpointLsn = 126000000953600034,
          FirstLsn = 126000000943800037,
          LastLsn = 126000000955200001,
          DatabaseName = "TestDb",
          ServerName = "ServerA",
          PhysicalDeviceName = @"\\DFS\BACKUP\ServerA\testDb\Testdb_backup_2018_10_29_000339_780.diff",
          StartTime = DateTime.Parse("2018-10-29 00:03:39.000")
        },
        new BackupMetadata {
          BackupType = BackupFileTools.BackupType.Log,
          DatabaseBackupLsn = 126000000882000037,
          CheckpointLsn = 126000000953600034,
          FirstLsn = 126000000955200001,
          LastLsn = 126000000955500001,
          DatabaseName = "TestDb",
          ServerName = "ServerA",
          PhysicalDeviceName = @"\\DFS\BACKUP\ServerA\testDb\Testdb_backup_2018_10_29_020007_343.trn",
          StartTime = DateTime.Parse("2018-10-29 02:00:07.000")
        }
      };
    }

    [Fact]
    public void BackupChainOrdered()
    {
      var agDatabase = new Mock<IAgDatabase>();
      agDatabase.Setup(agd => agd.RecentBackups()).Returns(_listBackups);
      var backupChain = new BackupChain(agDatabase.Object);

      var expected = _listBackups.OrderBy(bu => bu.FirstLsn);

      Assert.Equal<IEnumerable>(backupChain.OrderedBackups, expected);
    }

    [Fact]
    public void MissingLink()
    {
      var backups = ListBackups().Where(b => b.FirstLsn != 126000000955200001).ToList();
      var agDatabase = new Mock<IAgDatabase>();
      agDatabase.Setup(agd => agd.RecentBackups()).Returns(backups);

      var chain = new BackupChain(agDatabase.Object).OrderedBackups;
      Assert.NotEqual(chain.Last().LastLsn, ListBackups().Max(b => b.LastLsn));
    }

    // TODO: test skipping of logs if diff last LSN and log last LSN matches
    // TODO: test skipping of logs between diffs
    // TODO: test only keep last diff
  }
}
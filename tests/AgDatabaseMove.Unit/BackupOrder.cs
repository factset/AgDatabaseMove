namespace AgDatabaseMove.Unit
{
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Linq;
  using System.Security.Authentication;
  using Exceptions;
  using Moq;
  using Xunit;
  using Xunit.Sdk;


  public class BackupOrder
  {
    public BackupOrder()
    {
      _listBackups = ListBackups();
    }

    private static List<BackupMetadata> ListBackups()
    {
      return new List<BackupMetadata> {
        new BackupMetadata {
          BackupType = "L", DatabaseBackupLsn = 126000000943800037, CheckpointLsn = 126000000953600034,
          FirstLsn = 126000000955500001, LastLsn = 126000000955800001, DatabaseName = "TestDb", ServerName = "ServerB",
          PhysicalDeviceName = @"\\DFS\BACKUP\ServerB\testDb\Testdb_backup_2018_10_29_030006_660.trn",
          StartTime = DateTime.Parse("2018-10-29 03:00:06.000")
        },
        new BackupMetadata {
          BackupType = "D", DatabaseBackupLsn = 126000000882000037, CheckpointLsn = 126000000943800037,
          FirstLsn = 126000000936100001, LastLsn = 126000000945500001, DatabaseName = "TestDb", ServerName = "ServerA",
          PhysicalDeviceName = @"\\DFS\BACKUP\ServerA\testDb\Testdb_backup_2018_10_28_000227_200.full",
          StartTime = DateTime.Parse("2018-10-28 00:02:28.000")
        },
        new BackupMetadata {
          BackupType = "I", DatabaseBackupLsn = 126000000943800037, CheckpointLsn = 126000000953600034,
          FirstLsn = 126000000943800037, LastLsn = 126000000955200001, DatabaseName = "TestDb", ServerName = "ServerA",
          PhysicalDeviceName = @"\\DFS\BACKUP\ServerA\testDb\Testdb_backup_2018_10_29_000339_780.diff",
          StartTime = DateTime.Parse("2018-10-29 00:03:39.000")
        },
        new BackupMetadata {
          BackupType = "L", DatabaseBackupLsn = 126000000882000037, CheckpointLsn = 126000000953600034,
          FirstLsn = 126000000955200001, LastLsn = 126000000955500001, DatabaseName = "TestDb", ServerName = "ServerA",
          PhysicalDeviceName = @"\\DFS\BACKUP\ServerA\testDb\Testdb_backup_2018_10_29_020007_343.trn",
          StartTime = DateTime.Parse("2018-10-29 02:00:07.000")
        }
      };
    }

    private readonly List<BackupMetadata> _listBackups;

    [Fact]
    public void BackupChainOrdered()
    {
      var agDatabase = new Mock<IAgDatabase>();
      agDatabase.Setup(agd => agd.RecentBackups()).Returns(_listBackups);
      var backupChain = new BackupChain(agDatabase.Object);

      var expected = _listBackups.OrderBy(bu => bu.FirstLsn);

      Assert.Equal<IEnumerable>(backupChain.RestoreOrder, expected);
    }

    [Fact]
    public void MissingLink()
    {
      var backups = ListBackups().Where(b => b.FirstLsn != 126000000955200001).ToList();
      var agDatabase = new Mock<IAgDatabase>();
      agDatabase.Setup(agd => agd.RecentBackups()).Returns(backups);
      var ex = Assert.Throws<BackupChainException>(() => new BackupChain(agDatabase.Object));
    }

    // TODO: test skipping of logs if diff last LSN and log last LSN matches
    // TODO: test skipping of logs between diffs
    // TODO: test only keep last diff
  }
}
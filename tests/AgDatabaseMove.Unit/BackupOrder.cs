namespace AgDatabaseMove.Unit
{
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Linq;
  using Exceptions;
  using Moq;
  using SmoFacade;
  using Xunit;


  public class BackupOrder
  {

    private static IEnumerable<BackupMetadata> CloneBackupMetaDataList(List<BackupMetadata> list)
    {
      var result = new List<BackupMetadata>();
      list.ForEach(b => {
        result.Add((BackupMetadata)b.Clone());
      });
      result.Reverse();
      return result;
    }

    private static List<BackupMetadata> GetBackupList()
    {
      return new List<BackupMetadata> {
        new BackupMetadata {
          BackupType = BackupFileTools.BackupType.Log,
          DatabaseBackupLsn = 126000000943800037,
          CheckpointLsn = 126000000953600034,
          FirstLsn = 126000000955200001,
          LastLsn = 126000000955500001,
          DatabaseName = "TestDb",
          ServerName = "ServerA",
          PhysicalDeviceName = @"\\DFS\BACKUP\ServerA\testDb\Testdb_backup_2018_10_29_020007_343.trn",
          StartTime = DateTime.Parse("2018-10-29 02:00:07.000")
        },
        new BackupMetadata {
          BackupType = BackupFileTools.BackupType.Log,
          DatabaseBackupLsn = 126000000943800037,
          CheckpointLsn = 126000000953600034,
          FirstLsn = 126000000955800001,
          LastLsn = 126000000965800001,
          DatabaseName = "TestDb",
          ServerName = "ServerB",
          PhysicalDeviceName = @"\\DFS\BACKUP\ServerB\testDb\Testdb_backup_2018_10_29_040005_900.trn",
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
          BackupType = BackupFileTools.BackupType.Diff,
          DatabaseBackupLsn = 126000000943800037,
          CheckpointLsn = 126000000953600034,
          FirstLsn = 126000000943800038,
          LastLsn = 126000000955200001,
          DatabaseName = "TestDb",
          ServerName = "ServerA",
          PhysicalDeviceName = @"\\DFS\BACKUP\ServerA\testDb\Testdb_backup_2018_10_29_000339_780.diff",
          StartTime = DateTime.Parse("2018-10-29 00:03:39.000")
        }
      };
    }

    private static List<BackupMetadata> GetBackupListWithoutLogs()
    {
      var list = GetBackupList();
      list.RemoveAll(b => b.BackupType == BackupFileTools.BackupType.Log);
      return list;
    }
    
    private static List<BackupMetadata> GetBackupListWithoutDiff()
    {
      var list = GetBackupList();
      list.RemoveAll(b => b.BackupType == BackupFileTools.BackupType.Diff);
      return list;
    }

    private static List<BackupMetadata> GetBackupListWithStripes()
    {
      var list = GetBackupList();
      var listWithStripes = CloneBackupMetaDataList(list).ToList();
      listWithStripes.ForEach(b => {
        var path = b.PhysicalDeviceName.Split('.');
        b.PhysicalDeviceName = $"{path[0]}_striped.{path[1]}";
      });
      list.AddRange(listWithStripes);
      return list;
    }

    private static List<BackupMetadata> GetBackupListWithStripesAndDuplicates()
    {
      var listWithStripes = GetBackupListWithStripes();
      var duplicate = CloneBackupMetaDataList(listWithStripes);
      listWithStripes.AddRange(duplicate);
      return listWithStripes;
    }

    private static List<BackupMetadata> GetBackupListWithoutFull()
    {
      var list = GetBackupList();
      list.RemoveAll(b => b.BackupType == BackupFileTools.BackupType.Full);
      return list;
    }

    private static void VerifyListIsAValidBackupChain(List<BackupMetadata> backupChain)
    {
      var listOfFileNames = backupChain.Select(b => b.PhysicalDeviceName).ToList();
      Assert.True(listOfFileNames.Count == listOfFileNames.Distinct().Count(), "There are no duplicates?");

      var fullBackup = backupChain.First();
      Assert.NotNull(fullBackup);
      Assert.True(fullBackup.BackupType == BackupFileTools.BackupType.Full, "Is the first backup a full backup?");
      backupChain.RemoveAt(0);

      BackupMetadata currentBackup;
      var prevBackup = fullBackup;

      while((currentBackup = backupChain.FirstOrDefault())?.BackupType == BackupFileTools.BackupType.Full) {
        Assert.True(currentBackup.CheckpointLsn == prevBackup.CheckpointLsn,
                    "Is it a striped file of the full backup?");
        prevBackup = currentBackup;
        backupChain.RemoveAt(0);
      }

      prevBackup = currentBackup;
      while((currentBackup = backupChain.FirstOrDefault())?.BackupType == BackupFileTools.BackupType.Diff) {
        Assert.True(currentBackup.DatabaseBackupLsn == fullBackup.CheckpointLsn,
                    "Is the diff linked to the full backup?");
        Assert.True(currentBackup.FirstLsn == prevBackup.FirstLsn && currentBackup.LastLsn == prevBackup.LastLsn,
                    "Is it a striped file of the diff backup?");
        prevBackup = currentBackup;
        backupChain.RemoveAt(0);
      }

      prevBackup = currentBackup;
      while((currentBackup = backupChain.FirstOrDefault())?.BackupType == BackupFileTools.BackupType.Log) {
        Assert.True(currentBackup.DatabaseBackupLsn == fullBackup.CheckpointLsn,
                    "Is the log linked to the full backup?");
        Assert.True(
                    (currentBackup.FirstLsn == prevBackup.FirstLsn && currentBackup.LastLsn == prevBackup.LastLsn) ||
                    (prevBackup.LastLsn == currentBackup.FirstLsn),
                    "Is it either the striped file of the prev log backup or the next one in the chain?");

        prevBackup = currentBackup;
        backupChain.RemoveAt(0);
      }

      Assert.Empty(backupChain);
    }


    public static IEnumerable<object[]> PositiveTestData => new List<object[]> {
      new object[] { GetBackupList() },
      new object[] { GetBackupListWithStripes() },
      new object[] { GetBackupListWithStripesAndDuplicates() },
      new object[] { GetBackupListWithoutDiff() },
      new object[] { GetBackupListWithoutLogs() }
    };
    
    [Theory]
    [MemberData(nameof(PositiveTestData))]
    public void BackupChainIsCorrect(List<BackupMetadata> backupList)
    {
      var agDatabase = new Mock<IAgDatabase>();
      agDatabase.Setup(agd => agd.RecentBackups()).Returns(backupList);
      var backupChain = new BackupChain(agDatabase.Object);
      VerifyListIsAValidBackupChain(backupChain.OrderedBackups.ToList());
    }


    public static IEnumerable<object[]> NegativeTestData => new List<object[]> {
      new object[] { GetBackupListWithoutFull() },
      new object[] { new List<BackupMetadata>() }
    };

    [Theory]
    [MemberData(nameof(NegativeTestData))]
    public void CanDetectBackupChainIsWrong(List<BackupMetadata> backupList)
    {
      var agDatabase = new Mock<IAgDatabase>();
      agDatabase.Setup(agd => agd.RecentBackups()).Returns(backupList);
      Assert.Throws<BackupChainException>(() => new BackupChain(agDatabase.Object));
    }

    [Fact]
    public void MissingLink()
    {
      var backups = GetBackupList().Where(b => b.FirstLsn != 126000000955200001).ToList();
      var agDatabase = new Mock<IAgDatabase>();
      agDatabase.Setup(agd => agd.RecentBackups()).Returns(backups);

      var chain = new BackupChain(agDatabase.Object).OrderedBackups.ToList();
      Assert.NotEqual(chain.Last().LastLsn, GetBackupList().Max(b => b.LastLsn));
      VerifyListIsAValidBackupChain(chain);
    }

    // TODO: test skipping of logs if diff last LSN and log last LSN matches
    // TODO: test skipping of logs between diffs
    // TODO: test only keep last diff
  }
}
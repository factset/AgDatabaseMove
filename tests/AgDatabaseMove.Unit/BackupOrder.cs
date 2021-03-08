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
          FirstLsn = 126000000945600000,
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
      bool foundFull, foundDiff, foundLog;
      foundFull = foundDiff = foundLog = false;
      BackupMetadata full = null;
      BackupMetadata lastBackup = null;

      BackupMetadata currentBackup;
      while((currentBackup = backupChain.FirstOrDefault()) != null) {

        if(currentBackup.BackupType == BackupFileTools.BackupType.Full) {
          Assert.True(!foundFull && !foundDiff && !foundLog);
          foundFull = true;
          full = currentBackup;
        }
        else if(currentBackup.BackupType == BackupFileTools.BackupType.Diff) {
          Assert.True(foundFull && !foundDiff && !foundLog);
          Assert.Equal(full.CheckpointLsn, currentBackup.DatabaseBackupLsn);
          Assert.True(currentBackup.FirstLsn >= lastBackup.LastLsn);
          foundDiff = true;
        }
        else if(currentBackup.BackupType == BackupFileTools.BackupType.Log) {
          Assert.True(foundFull);
          Assert.True(currentBackup.FirstLsn >= lastBackup.LastLsn);
          foundLog = true;
        }

        lastBackup = currentBackup;
        backupChain.RemoveAll(b => b.LastLsn == currentBackup.LastLsn);
      }
    }

    public static IEnumerable<object[]> PositiveTestData => new List<object[]> {
      new object[] { GetBackupList() },
      new object[] { GetBackupListWithStripes() },
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

    [Fact]
    public void DuplicateFiles()
    {
      var backups = GetBackupListWithStripesAndDuplicates();
      var agDatabase = new Mock<IAgDatabase>();
      agDatabase.Setup(agd => agd.RecentBackups()).Returns(backups);
      var chain = new BackupChain(agDatabase.Object).OrderedBackups.ToList();
      Assert.Equal(backups.GroupBy(b => b.PhysicalDeviceName).Count(), chain.Count);
    }

    // TODO: test skipping of logs if diff last LSN and log last LSN matches
    // TODO: test skipping of logs between diffs
    // TODO: test only keep last diff
  }
}
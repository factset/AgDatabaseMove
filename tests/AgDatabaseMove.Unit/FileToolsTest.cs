namespace AgDatabaseMove.Unit
{
  using System.Collections.Generic;
  using SmoFacade;
  using Xunit;


  public class BackupFileToolsTest
  {
    public static IEnumerable<object[]> UrlFileExamples => new List<object[]> {
      new object[] { "https://hello/a.bak" },
      new object[] { "https://hello/a.full" },
      new object[] { "https://storage-account.blob.core.windows.net/container/file.trn" },
      new object[] { "https://hello/a.diff" },
      new object[] { "https://a.diff" },
      new object[] { "https://1/2/3/4/5/a.diff" },
      new object[] { "https://storage-account.blob.core.windows.net/container/file.bad" },
      new object[]
        { "https://storage-account.blob.core.windows.net/container/sql/db_name/backup_2020_09_02_170003_697.trn" },
      new object[] { "http://hello/a.bak" }
    };

    public static IEnumerable<object[]> NonUrlFileExamples => new List<object[]> {
      new object[] { @"c:\hello\a.bak" },
      new object[] { @"\\abc\hello/a.bak" },
      new object[] { "https://storage-account.blob.core.windows.net/container" },
      new object[] { "http://storage-account.blob.core.windows.net/container" }
    };

    [Theory]
    [MemberData(nameof(UrlFileExamples))]
    public void UrlFilesAreUrl(string file)
    {
      Assert.True(BackupFileTools.IsUrl(file));
    }

    [Theory]
    [MemberData(nameof(NonUrlFileExamples))]
    public void NonUrlFilesAreNotUrl(string file)
    {
      Assert.False(BackupFileTools.IsUrl(file));
    }

    [Theory]
    [InlineData(BackupFileTools.BackupType.Log, "trn")]
    [InlineData(BackupFileTools.BackupType.Diff, "diff")]
    [InlineData(BackupFileTools.BackupType.Full, "bak")]
    public void BackupTypeToExtensionTest(BackupFileTools.BackupType type, string ext)
    {
      Assert.Equal(ext, BackupFileTools.BackupTypeToExtension(type));
    }

    [Theory]
    [InlineData("L", BackupFileTools.BackupType.Log)]
    [InlineData("I", BackupFileTools.BackupType.Diff)]
    [InlineData("D", BackupFileTools.BackupType.Full)]
    public void BackupTypeAbbrevToType(string abbrev, BackupFileTools.BackupType type)
    {
      Assert.Equal(type, BackupFileTools.BackupTypeAbbrevToType(abbrev));
    }
  }
}
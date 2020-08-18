namespace AgDatabaseMove.Unit
{
  using System;
  using System.Collections;
  using System.Collections.Generic;
  using System.Linq;
  using Moq;
  using Xunit;
  using Xunit.Extensions;


  public class FileToolsTest
  {

    public static IEnumerable<object[]> UrlFileExamples => new List<object[]>
    {
      new object[] { "https://hello/a.bak" },
      new object[] { "https://hello/a.full" },
      new object[] { "https://storage-account.blob.core.windows.net/container/file.trn" },
      new object[] { "https://hello/a.diff" },
      new object[] { "https://a.diff" },
      new object[] { "https://1/2/3/4/5/a.diff" }
    };
    
    public static IEnumerable<object[]> NonUrlFileExamples => new List<object[]>
    {
      new object[] { @"c:\hello\a.bak" },
      new object[] { @"\\abc\hello/a.bak" },
      new object[] { "http://hello/a.bak" },
      new object[] { "https://storage-account.blob.core.windows.net/container" },
      new object[] { "https://storage-account.blob.core.windows.net/container/file.bad" },
    };

    public FileToolsTest() { }

    [Theory, MemberData(nameof(UrlFileExamples))]
    public void UrlFilesAreUrl(string file)
    {
      Assert.True(FileTools.IsUrl(file));
    }

    [Theory, MemberData(nameof(NonUrlFileExamples))]
    public void NonUrlFilesAreNotUrl(string file)
    {
      Assert.False(FileTools.IsUrl(file));
    }

    [Theory]
    [InlineData(FileTools.BackupType.Log, "trn")]
    [InlineData(FileTools.BackupType.Diff, "diff")]
    [InlineData(FileTools.BackupType.Full, "bak")]
    public void BackupTypeToExtensionTest(FileTools.BackupType type, string ext)
    {
      Assert.Equal(ext, FileTools.BackupTypeToExtension(type));
    }

    [Theory]
    [InlineData("L", FileTools.BackupType.Log)]
    [InlineData("I", FileTools.BackupType.Diff)]
    [InlineData("D", FileTools.BackupType.Full)]
    public void BackupTypeAbbrevToType(string abbrev, FileTools.BackupType type)
    {
      Assert.Equal(type, FileTools.BackupTypeAbbrevToType(abbrev));
    }


  }
}
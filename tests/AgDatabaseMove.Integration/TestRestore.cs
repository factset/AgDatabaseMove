namespace AgDatabaseMove.Integration
{
  using System;
  using System.Collections.Generic;
  using System.Data.SqlClient;
  using System.Diagnostics;
  using System.Linq;
  using System.Text.RegularExpressions;
  using Microsoft.Extensions.Configuration;
  using SmoFacade;
  using Xunit;


  public class TestRestoreConfig
  {
    public DatabaseConfig From { get; set; }
    public DatabaseConfig To { get; set; }
  }

  public class TestRestoreFixture : IDisposable
  {
    public readonly List<LoginProperties> _preTestLogins;
    public readonly AgDatabase _source;
    public readonly AgDatabase _test;
    public TestRestoreConfig _config;

    public TestRestoreFixture()
    {
      var builder = new ConfigurationBuilder()
        .AddJsonFile("config.json", false);

      _config = builder.Build().GetSection("TestRestore").Get<TestRestoreConfig>();

      _source = new AgDatabase(_config.From);
      _test = new AgDatabase(_config.To);
      _test.Delete();

      // We only snapshot the primary instance's logins. It works for our integration environment, but we could do better and snapshot each instance's.
      _preTestLogins = _test._listener.Primary.Logins.Select(l => l.Properties()).ToList();
    }

    public void Dispose()
    {
      if(_test != null) {
        _test._listener.ForEachAgInstance(CleanupLogins);
        _test.Dispose();
      }

      _source?.Dispose();
    }

    private void CleanupLogins(Server server)
    {
      var postTestLogins = server.Logins.ToList();
      var newLogins = postTestLogins.Where(post => _preTestLogins.All(pre => pre.Name != post.Name));
      foreach(var login in newLogins)
        login.Drop();
    }
  }

  public class TestRestore : IClassFixture<TestRestoreFixture>
  {
    public TestRestore(TestRestoreFixture testRestoreFixture)
    {
      _testRestoreFixture = testRestoreFixture;
    }

    private readonly TestRestoreFixture _testRestoreFixture;

    private AgDatabase Test => _testRestoreFixture._test;
    private AgDatabase Source => _testRestoreFixture._source;
    private TestRestoreConfig Config => _testRestoreFixture._config;

    private string RestoreFileRelocator(string name)
    {
      // Our integration environment is currently mixed.
      name = Regex.Replace(name, "MSSQL11", "MSSQL13", RegexOptions.IgnoreCase & RegexOptions.CultureInvariant);
      name = Regex.Replace(name, Source.Name, Test.Name, RegexOptions.IgnoreCase & RegexOptions.CultureInvariant);
      return name;
    }


    [Fact]
    public void DetectsInitializing()
    {
      // Restore databases across AG instances
      Assert.False(Test.Exists());
      var restore = new Restore(Source, Test) { Finalize = false, FileRelocator = RestoreFileRelocator };
      restore.AgDbRestore();

      // Restore with recovery primary
      Test.FinalizePrimary();

      // Write sufficient data to the primary
      var connectionStringBuilder = new SqlConnectionStringBuilder(_testRestoreFixture._config.To.ConnectionString);
      connectionStringBuilder.InitialCatalog = Test.Name;
      using(var connection = new SqlConnection(connectionStringBuilder.ToString())) {
        connection.Open();
        var createTableSql =
          "CREATE TABLE TestSync (Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY, script VARCHAR(MAX) NULL)";
        using(var createTable = new SqlCommand(createTableSql, connection)) {
          createTable.ExecuteNonQuery();
        }

        var fillTableSql =
          "INSERT INTO TestSync (script) (SELECT TOP 10000 sm.[definition] FROM sys.all_sql_modules AS sm CROSS JOIN sys.all_sql_modules AS asm)";
        using(var fillTable = new SqlCommand(fillTableSql, connection)) {
          fillTable.ExecuteNonQuery();
        }
      }

      Test.JoinAg();

      Assert.True(Test.IsInitializing());

      Test.Delete();
    }


    /// <summary>
    ///   This is how I envision the library being used to do our move operation while clients are using the database.
    /// </summary>
    [Fact]
    public void ProgressiveRestore()
    {
      var restore = new Restore(Source, Test) { FileRelocator = RestoreFileRelocator };
      int seconds;
      decimal? lastLsn = null;

      // Restore the database and subsequent log files 
      do {
        var timer = new Stopwatch();
        timer.Start();
        Source.LogBackup();
        lastLsn = restore.AgDbRestore(lastLsn);
        timer.Stop();
        seconds = timer.Elapsed.Seconds;
      } while(seconds > 5);

      // Do things here to disconnect users: Set single user mode, signal the service etc.
      Source.RestrictedUserMode();

      // Hopefully a quick backup and restore.
      Source.LogBackup();
      restore.Finalize = true;
      restore.AgDbRestore(lastLsn);
      // The database is migrated

      Source.MultiUserMode();
      Test.Delete();
    }

    [Fact]
    public void RestoreAndCleanup()
    {
      Assert.False(Test.Exists());
      var restore = new Restore(Source, Test)
        { Finalize = true, CopyLogins = true, FileRelocator = RestoreFileRelocator };
      restore.AgDbRestore();
      Assert.True(Test.Exists());
      Test.Delete();
      Assert.False(Test.Exists());
    }
  }
}
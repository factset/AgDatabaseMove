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
    public string FullBackup { get; set; }
    public string TestQuery { get; set; }
  }

  public class TestRestoreFixture : IDisposable
  {
    public readonly Dictionary<string, List<LoginProperties>> _preTestLogins = new Dictionary<string, List<LoginProperties>>();
    public readonly AgDatabase _source;
    public readonly AgDatabase _test;
    public TestRestoreConfig _config;

    public TestRestoreFixture()
    {
      var builder = new ConfigurationBuilder().AddJsonFile("config.json", false);

      _config = builder.Build().GetSection("TestRestore").Get<TestRestoreConfig>();

      _source = new AgDatabase(_config.From);
      _test = new AgDatabase(_config.To);
      _test.Delete();

      // We only snapshot the primary instance's logins. It works for our integration environment, but we could do better and snapshot each instance's.
      _test.Listener.ForEachAgInstance(server => {
        _preTestLogins.Add(server.Name, server.Logins.Select(l => l.Properties()).ToList());
      });

    }

    public void Dispose()
    {
      if(_test != null) {
        _test.Listener.ForEachAgInstance(CleanupLogins);
        _test.Dispose();
      }

      _source?.Dispose();
    }

    private void CleanupLogins(Server server)
    {
      var postTestLogins = server.Logins.ToList();
      var newLogins = postTestLogins.Where(post => _preTestLogins[server.Name].All(pre => pre.Name != post.Name));
      foreach(var login in newLogins) login.Drop();
    }
  }

  public class TestRestore : IClassFixture<TestRestoreFixture>
  {
    private readonly TestRestoreFixture _testRestoreFixture;

    public TestRestore(TestRestoreFixture testRestoreFixture)
    {
      _testRestoreFixture = testRestoreFixture;
    }

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
      // AgDatabaseMove databases across AG instances
      Assert.False(Test.Exists());

      var mover = new AgDatabaseMove(new MoveOptions {
        Source = Source,
        Destination = Test,
        Overwrite = false,
        Finalize = false,
        FileRelocator = RestoreFileRelocator,
        RetryDuration = attempt => TimeSpan.FromSeconds(attempt * 10)
      });

      mover.Move();

      // AgDatabaseMove with recovery primary
      Test.FinalizePrimary();

      // Write sufficient data to the primary
      var connectionStringBuilder = new SqlConnectionStringBuilder(_testRestoreFixture._config.To.ConnectionString);
      connectionStringBuilder.InitialCatalog = Test.Name;
      using var connection = new SqlConnection(connectionStringBuilder.ToString());
      connection.Open();
      var createTableSql =
        "CREATE TABLE TestSync (Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY, script VARCHAR(MAX) NULL)";
      using var createTable = new SqlCommand(createTableSql, connection);
      createTable.ExecuteNonQuery();

      var fillTableSql =
        "INSERT INTO TestSync (script) (SELECT TOP 100000 sm.[definition] FROM sys.all_sql_modules AS sm CROSS JOIN sys.all_sql_modules AS asm)";
      using var fillTable = new SqlCommand(fillTableSql, connection);
      fillTable.ExecuteNonQuery();

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
      var mover = new AgDatabaseMove(new MoveOptions {
        Source = Source,
        Destination = Test,
        Overwrite = false,
        Finalize = false,
        FileRelocator = RestoreFileRelocator,
        RetryDuration = attempt => TimeSpan.FromSeconds(attempt * 10)
      });

      int seconds;
      decimal? lastLsn = null;

      // AgDatabaseMove the database and subsequent log files 
      do {
        var timer = new Stopwatch();
        timer.Start();
        lastLsn = mover.Move(lastLsn);
        timer.Stop();
        seconds = timer.Elapsed.Seconds;
      } while(seconds > 5);

      // Do things here to disconnect users: Set single user mode, signal the service etc.
      Source.RestrictedUserMode();

      // Hopefully a quick backup and restore
      mover._options.Finalize = true;
      mover.Move(lastLsn);

      // The database is migrated
      Source.MultiUserMode();
      Test.Delete();
    }

    [Fact]
    public void RestoreAndCleanup()
    {
      Assert.False(Test.Exists());

      var mover = new AgDatabaseMove(new MoveOptions {
        Source = Source,
        Destination = Test,
        Finalize = true,
        CopyLogins = true,
        FileRelocator = RestoreFileRelocator,
        RetryDuration = attempt => TimeSpan.FromSeconds(attempt * 10)
      });

      mover.Move();
      Assert.True(Test.Exists());

      Test.Delete();
      Assert.False(Test.Exists());
    }

    [Fact]
    public void TestSimpleRecovery()
    {
      var fullBackup = new SingleBackup { PhysicalDeviceName = Config.FullBackup, BackupType = BackupFileTools.BackupType.Full};
      Func<int, TimeSpan> retryDurationProvider = attempt => TimeSpan.FromSeconds(10 * attempt);
      Test.Restore(fullBackup, retryDurationProvider);
      Test.JoinAg();
      Assert.True(Test.Exists());

      // Assert database has data from full backup.
      var connectionStringBuilder = new SqlConnectionStringBuilder(Config.To.ConnectionString);
      connectionStringBuilder.InitialCatalog = Test.Name;
      using var connection = new SqlConnection(connectionStringBuilder.ToString());
      connection.Open();
      using var testQuery = new SqlCommand(Config.TestQuery, connection);
      var reader = testQuery.ExecuteReader();
      Assert.True(reader.HasRows);

      Test.Delete();
    }

    [Fact]
    public void TestThrowsIfBackupNotFull()
    {
      var logBackup = new SingleBackup { PhysicalDeviceName = Config.FullBackup, BackupType = BackupFileTools.BackupType.Log };
      var diffBackup = new SingleBackup { PhysicalDeviceName = Config.FullBackup, BackupType = BackupFileTools.BackupType.Diff };
      Func<int, TimeSpan> retryDurationProvider = attempt => TimeSpan.FromSeconds(10 * attempt);

      Assert.Throws<ArgumentException>(() => Test.Restore(logBackup, retryDurationProvider));
      Assert.Throws<ArgumentException>(() => Test.Restore(diffBackup, retryDurationProvider));
    }
 }
}
using AgDatabaseMove.Integration.Config;
using System;

namespace AgDatabaseMove.Integration.Fixtures
{
  public class TestAgDatabaseFixture : TestConfiguration<TestAgDatabaseConfig>, IDisposable
  {
    public TestConfiguration<TestLoginConfig> _login;

    public AgDatabase _agDatabase;

    public TestAgDatabaseFixture() : base("TestAgDatabase")
    {
      _agDatabase = ConstructAgDatabase();
      _login = new TestConfiguration<TestLoginConfig>("TestLogin");
    }

    // Public fields for testing new sql login creation.
    public string _loginPassword => _login._config.Password;

    public string _loginName => _login._config.LoginName;

    public string _loginDefaultDatabase => _login._config.DefaultDatabase;

    public AgDatabase ConstructAgDatabase()
    {
      var dbConfig = new DatabaseConfig
      {
        BackupPathSqlQuery = _config.BackUpPathSqlQuery,
        ConnectionString = _config.ConnectionString,
        DatabaseName = _config.DatabaseName,
        CredentialName = _config.CredentialName
      };

      return new AgDatabase(dbConfig);
    }
    public void Dispose()
    {
      // Cleanup new Sql logins created.
      _agDatabase._listener.Primary._server.Logins[_loginName]?.DropIfExists();
      foreach(var server in _agDatabase._listener.Secondaries) {
        server._server.Logins[_loginName]?.DropIfExists();
      }

      _agDatabase.Dispose();
    }
  }
}

namespace AgDatabaseMove.Integration.Fixtures
{
  using Config;
  using System;
  using System.Collections.Generic;
  using Microsoft.SqlServer.Management.Smo;
  using SmoFacade;
  using System.Linq;

  public class TestAgDatabaseFixture : IDisposable
  {
    public readonly TestAgDatabaseConfig _agConfig =
      new TestConfiguration<TestAgDatabaseConfig>("TestAgDatabase")._config;

    public readonly TestLoginConfig _loginConfig =
      new TestConfiguration<TestLoginConfig>("TestLogin")._config;

    public AgDatabase _agDatabase;

    public IEnumerable<SmoFacade.Login> _createdLogins;

    public TestAgDatabaseFixture()
    {
      _agDatabase = new AgDatabase(new DatabaseConfig
      {
        BackupPathSqlQuery = _agConfig.BackUpPathSqlQuery,
        ConnectionString = _agConfig.ConnectionString,
        DatabaseName = _agConfig.DatabaseName,
        CredentialName = _agConfig.CredentialName
      });

      _agDatabase.AddLogin(new LoginProperties
      {
        Name = _loginConfig.LoginName,
        Password = _loginConfig.Password,
        LoginType = LoginType.SqlLogin,
        DefaultDatabase = _loginConfig.DefaultDatabase
      });

      _agDatabase.AddUser(new UserProperties
      {
        Name = _loginConfig.LoginName,
        LoginName = _loginConfig.LoginName,
        Roles = new[] { new RoleProperties { Name = "db_datareader" } },
        Permissions = new DatabasePermissionSet(DatabasePermission.Execute)
      });

      _createdLogins = GetCreatedLogins();
    }

    private IEnumerable<SmoFacade.Login> GetCreatedLogins()
    {
      List<SmoFacade.Login> logins = new List<SmoFacade.Login>();
      logins.Add(_agDatabase._listener.Primary.Logins
                   .SingleOrDefault(l => l.Name.Equals(_loginConfig.LoginName, StringComparison.InvariantCultureIgnoreCase)));
      foreach (var server in _agDatabase._listener.Secondaries)
      {
        logins.Add(server.Logins
                     .SingleOrDefault(l => l.Name.Equals(_loginConfig.LoginName, StringComparison.InvariantCultureIgnoreCase)));
      }

      return logins;
    }

    public void Dispose()
    {
      _agDatabase?.DropLogin(new LoginProperties { Name = _loginConfig.LoginName });
      _agDatabase?.DropUser(new UserProperties { Name = _loginConfig.LoginName });
      _agDatabase?.Dispose();
    }
  }
}

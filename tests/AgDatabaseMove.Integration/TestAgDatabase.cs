using AgDatabaseMove.Integration.Fixtures;
using AgDatabaseMove.SmoFacade;
using System.Collections.Generic;
using Xunit;
using Smo = Microsoft.SqlServer.Management.Smo;

namespace AgDatabaseMove.Integration
{
  public class TestAgDatabase : IClassFixture<TestAgDatabaseFixture>
  {
    private readonly TestAgDatabaseFixture _testAgDatabaseFixture;

    public TestAgDatabase(TestAgDatabaseFixture testAgDatabaseFixture)
    {
      _testAgDatabaseFixture = testAgDatabaseFixture;
    }

    private AgDatabase AgDatabase => _testAgDatabaseFixture._agDatabase;

    private string LoginName => _testAgDatabaseFixture._loginName;

    private string LoginPassword => _testAgDatabaseFixture._loginPassword;

    private string DefaultDatabase => _testAgDatabaseFixture._loginDefaultDatabase;

    private void CreateNewSqlLogin(LoginProperties login)
    {
      AgDatabase.AddLogin(login);
    }

    private void TestLoginExistsOnAg(List<Smo.Login> createdLogins)
    {
      createdLogins.ForEach(Assert.NotNull);
    }

    private List<Smo.Login> GetCreatedLogins()
    {
      List<Smo.Login> logins = new List<Smo.Login>();
      logins.Add(AgDatabase._listener.Primary._server.Logins[LoginName]);
      foreach(var server in AgDatabase._listener.Secondaries)
      {
        logins.Add(server._server.Logins[LoginName]);
      }

      // Verifying login was created on more than primary.
      Assert.True(logins.Count > 1);
      return logins;
    }

    private void TestLoginSidsMatch(List<Smo.Login> createdLogins)
    {
      for (int i = 0; i < createdLogins.Count - 1; i++)
      {
        Assert.Equal(createdLogins[i].Sid, createdLogins[i + 1].Sid);
      }
    }

    [Fact]
    public void CreateNewSqlLoginTest()
    {
      var newSqlLogin = new LoginProperties
      {
        Name = LoginName,
        Password = LoginPassword,
        LoginType = Smo.LoginType.SqlLogin,
        DefaultDatabase = DefaultDatabase
      };

      CreateNewSqlLogin(newSqlLogin);
      var createdLogins = GetCreatedLogins();
      TestLoginExistsOnAg(createdLogins);
      TestLoginSidsMatch(createdLogins);
    }
  }
}

namespace AgDatabaseMove.Integration
{
  using System;
  using System.Linq;
  using Fixtures;
  using Microsoft.SqlServer.Management.Smo;
  using SmoFacade;
  using Xunit;
  using Login = SmoFacade.Login;
  using Server = SmoFacade.Server;


  public class TestLogin : IClassFixture<TestLoginFixture>
  {
    public TestLogin(TestLoginFixture testLoginFixture)
    {
      _testLoginFixture = testLoginFixture;
    }

    private readonly TestLoginFixture _testLoginFixture;

    private Server Server => _testLoginFixture._server;
    private string Password => _testLoginFixture.Password;
    private string LoginName => _testLoginFixture.LoginName;
    private string DefaultDatabase => _testLoginFixture.DefaultDatabase;

    private void ConnectViaLogin(string loginName, string password)
    {
      var connectionStringBuilder = _testLoginFixture.ConnectionStringBuilder;
      connectionStringBuilder.IntegratedSecurity = false;
      connectionStringBuilder.InitialCatalog = "master";
      connectionStringBuilder.UserID = loginName;
      connectionStringBuilder.Password = password;
    }

    /// <summary>
    ///   Opens a new server object so we're certain the SMO caching isn't faking us out.
    /// </summary>
    /// <param name="login"></param>
    private void TestLoginExists(Login login)
    {
      using(var testServer = _testLoginFixture.ConstructServer()) {
        var existingLogin =
          testServer.Logins.SingleOrDefault(l => l.Name.Equals(login.Name,
                                                               StringComparison.InvariantCultureIgnoreCase));
        Assert.NotNull(existingLogin);
      }
    }

    private Login CreateTestLogin(LoginProperties loginProperties)
    {
      return new Login(loginProperties, Server);
    }

    [Fact]
    public void CreateLoginAndCopy()
    {
      var newLoginProperties = new LoginProperties {
        Sid = new byte[]
          { 0xEA, 0x11, 0x6E, 0x4D, 0x2A, 0x9E, 0x43, 0x4B, 0x84, 0x04, 0xB9, 0x93, 0xF4, 0x8F, 0x1E, 0xA5 },
        Name = LoginName,
        Password = Password,
        LoginType = LoginType.SqlLogin,
        DefaultDatabase = DefaultDatabase
      };

      var initialLogin = CreateTestLogin(newLoginProperties);
      TestLoginExists(initialLogin);

      // These login properties will have the password hash
      var loginProperties = initialLogin.Properties();

      initialLogin.Drop();

      var copiedLogin = CreateTestLogin(loginProperties);
      TestLoginExists(copiedLogin);
      Assert.Equal(newLoginProperties.DefaultDatabase, copiedLogin.Properties().DefaultDatabase);

      ConnectViaLogin(LoginName, Password);
    }
  }
}
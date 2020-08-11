namespace AgDatabaseMove.Integration.Fixtures
{
  using System;
  using System.Data.SqlClient;
  using System.Linq;
  using Config;
  using SmoFacade;


  public class TestLoginFixture : TestConfiguration<TestLoginConfig>, IDisposable
  {
    public Server _server;

    public TestLoginFixture() : base("TestLogin")
    {
      _server = ConstructServer();
    }

    public string Password => _config.Password;

    public string LoginName => _config.LoginName;

    public SqlConnectionStringBuilder ConnectionStringBuilder =>
      new SqlConnectionStringBuilder(_config.ConnectionString);

    public string DefaultDatabase => _config.DefaultDatabase;

    public void Dispose()
    {
      _server.Logins.SingleOrDefault(l => l.Name.Equals(LoginName, StringComparison.InvariantCultureIgnoreCase))
        ?.Drop();
      _server?.Dispose();
    }

    public Server ConstructServer()
    {
      return new Server(_config.ConnectionString);
    }
  }
}
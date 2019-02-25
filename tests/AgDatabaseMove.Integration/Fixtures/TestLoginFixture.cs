namespace AgDatabaseMove.Integration.Fixtures
{
  using System;
  using System.Data.SqlClient;
  using Config;
  using Microsoft.Extensions.Configuration;
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

    public void Dispose()
    {
      _server?.Dispose();
    }

    public Server ConstructServer()
    {
      return new Server(_config.ConnectionString);
    }
  }
}
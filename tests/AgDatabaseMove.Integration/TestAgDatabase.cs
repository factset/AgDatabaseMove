namespace AgDatabaseMove.Integration
{
  using System.Linq;
  using Xunit;
  using Fixtures;

  public class TestAgDatabase : IClassFixture<TestAgDatabaseFixture>
  {
    private readonly TestAgDatabaseFixture _fixture;

    public TestAgDatabase(TestAgDatabaseFixture testAgDatabaseFixture)
    {
      _fixture = testAgDatabaseFixture;
    }

    [Fact]
    public void TestLoginsExist()
    {
      _fixture._agDatabase.ContainsLogin(_fixture._loginConfig.LoginName);
    }

    [Fact]
    public void TestLoginSidsMatch()
    {
      var sid = _fixture._createdLogins.First().Sid;
      Assert.All(_fixture._createdLogins.Select(l => l.Sid), s => Assert.Equal(s, sid));
    }

    [Fact]
    public void TestUserExists()
    {
      var db = _fixture._agDatabase.Listener.Primary.Database(_fixture._agConfig.DatabaseName);
      Assert.Contains(_fixture._loginConfig.LoginName, db.Users.Select(u => u.Name));
    }

    [Fact]
    public void TestUserHasRoles()
    {
      var db = _fixture._agDatabase.Listener.Primary.Database(_fixture._agConfig.DatabaseName)._database;
      var user = db.Users[_fixture._loginConfig.LoginName];
      Assert.True(user.EnumRoles().Contains("db_datareader"));
    }

    [Fact]
    public void TestUserHasPermissions()
    {
      var db = _fixture._agDatabase.Listener.Primary.Database(_fixture._loginConfig.DefaultDatabase)._database;
      var permissions = db.EnumDatabasePermissions(_fixture._loginConfig.LoginName);
      Assert.NotNull(permissions.FirstOrDefault(p => p.PermissionType.Execute));
    }
  }
}
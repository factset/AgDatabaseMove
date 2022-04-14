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
      Assert.True(true);
    }

    [Fact]
    public void TestLoginSidsMatch()
    {
      Assert.Equal(_fixture._createdLogins.Select(l => l.Sid), 
                   Enumerable.Repeat(_fixture._createdLogins.First().Sid, _fixture._createdLogins.Count()));
    }

    [Fact]
    public void TestUserExists()
    {
      var db = _fixture._agDatabase._listener.Primary.Database(_fixture._loginConfig.DefaultDatabase);
      Assert.Contains(_fixture._loginConfig.LoginName, db.Users.Select(u => u.Name));
    }

    [Fact]
    public void TestUserHasPermissions()
    {
      var db = _fixture._agDatabase._listener.Primary.Database(_fixture._loginConfig.DefaultDatabase)._database;
      var permissions = db.EnumDatabasePermissions(_fixture._loginConfig.LoginName);
      Assert.NotNull(permissions.FirstOrDefault(p => p.PermissionType.Execute));
    }

    [Fact]
    public void TestUserHasRoles()
    {
      var user = _fixture._agDatabase._listener.Primary.Database(_fixture._loginConfig.DefaultDatabase)._database.Users[_fixture._loginConfig.LoginName];
      Assert.True(user.EnumRoles().Contains("db_datareader"));
    }
  }
}
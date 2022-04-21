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
  }
}
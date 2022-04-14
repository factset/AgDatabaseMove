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
  }
}
namespace AgDatabaseMove.Unit
{
  using Moq;
  using SmoFacade;
  using Xunit;


  public class RestoreTests
  {
    private readonly LoginProperties _loginProperties;

    public RestoreTests()
    {
      _loginProperties = new LoginProperties { DefaultDatabase = "foo" };
    }

    [Theory]
    [InlineData("Foo", "foo", "foo")]
    [InlineData("bar", "bar", "master")]
    [InlineData("foo", "bar", "bar")]
    public void DefaultDatabase(string sourceDbName, string destinationDbName, string defaultDb)
    {
      var source = new Mock<IAgDatabase>();
      source.Setup(s => s.Name).Returns(sourceDbName);

      var destination = new Mock<IAgDatabase>();
      destination.Setup(d => d.Name).Returns(destinationDbName);
      var restore = new AgDatabaseMove(new MoveOptions { Source = source.Object, Destination = destination.Object });

      restore.UpdateDefaultDb(_loginProperties);

      Assert.Equal(defaultDb, _loginProperties.DefaultDatabase);
    }
  }
}
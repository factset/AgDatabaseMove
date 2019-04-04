namespace AgDatabaseMove.Unit
{
  using System.Collections.Generic;
  using Moq;
  using SmoFacade;
  using Xunit;


  public class RestoreTests
  {
    [Fact]
    public void DefaultDatabase()
    {
      var loginProperties = new LoginProperties {
        DefaultDatabase = "foo"
      };

      var source = new Mock<IAgDatabase>();
      source.Setup(s => s.Name).Returns("foo");

      var destination = new Mock<IAgDatabase>();
      destination.Setup(d => d.Name).Returns("foo");
      var restore = new Restore(source.Object, destination.Object);

      restore.UpdateDefaultDb(loginProperties);

      Assert.Equal("foo", loginProperties.DefaultDatabase);
    }

    [Fact]
    public void DefaultDatabaseNulled()
    {
      var loginProperties = new LoginProperties {
        DefaultDatabase = "foo"
      };
      
      var source = new Mock<IAgDatabase>();
      source.Setup(s => s.Name).Returns("bar");

      var destination = new Mock<IAgDatabase>();
      destination.Setup(d => d.Name).Returns("baz");
      var restore = new Restore(source.Object, destination.Object);

      restore.UpdateDefaultDb(loginProperties);

      Assert.Null(loginProperties.DefaultDatabase);
    }

    [Fact]
    public void DefaultDatabaseRenamed()
    {
      var loginProperties = new LoginProperties {
        DefaultDatabase = "foo"
      };

      var source = new Mock<IAgDatabase>();
      source.Setup(s => s.Name).Returns("foo");
      
      var destination = new Mock<IAgDatabase>();
      destination.Setup(d => d.Name).Returns("bar");

      var restore = new Restore(source.Object, destination.Object);

      restore.UpdateDefaultDb(loginProperties);

      Assert.Equal("bar", loginProperties.DefaultDatabase);
    }
  }
}
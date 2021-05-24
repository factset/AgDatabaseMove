namespace AgDatabaseMove.Unit
{
  using System.Collections.Generic;
  using SmoFacade;
  using Xunit;


  public class SmoFacadeListenerTest
  {
    /// <summary>
    ///   Tests for Listener.SplitDomainAndPort()
    /// </summary>
    private const string DOMAIN = "abc.def.ghi";

    private const string HOST = "abc";
    private const string PORT = ",123";
    private const string NAMED_INSTANCE = "\\SQL";
    private const string BAD_PORT = ":123";
    private const string BAD_NAMED_INSTANCE = "SQL";


    /// <summary>
    ///   Tests for Listener.GetPreferredPort()
    /// </summary>
    private const string IPort = ",123";

    private const string INamed = "\\SQL";

    private const string LPort = ",321";
    private const string LNamed = "\\LQS";

    // {input, expectedDomain, expectedPort (can be null)}
    public static IEnumerable<object[]> ValidPorts => new List<object[]> {
      new object[] { DOMAIN, DOMAIN, null },
      new object[] { $"{DOMAIN}{PORT}", DOMAIN, PORT },
      new object[] { $"{DOMAIN}{NAMED_INSTANCE}", DOMAIN, NAMED_INSTANCE },

      new object[] { HOST, HOST, null },
      new object[] { $"{HOST}{PORT}", HOST, PORT },
      new object[] { $"{HOST}{NAMED_INSTANCE}", HOST, NAMED_INSTANCE }
    };

    // {input, expectedDomain, expectedPort (can be null)}
    public static IEnumerable<object[]> InvalidPorts => new List<object[]> {
      new object[] { $"{DOMAIN}{BAD_PORT}", $"{DOMAIN}{BAD_PORT}", null },
      new object[] { $"{DOMAIN}{BAD_NAMED_INSTANCE}", $"{DOMAIN}{BAD_NAMED_INSTANCE}", null },

      new object[] { $"{HOST}{BAD_PORT}", $"{HOST}{BAD_PORT}", null },
      new object[] { $"{HOST}{BAD_NAMED_INSTANCE}", $"{HOST}{BAD_NAMED_INSTANCE}", null }
    };

    [Theory]
    [MemberData(nameof(ValidPorts))]
    public void ValidPortTests(string input, string expectedDomain, string expectedPort = null)
    {
      var (domain, port) = Listener.SplitDomainAndPort(input);
      Assert.Equal(domain, expectedDomain);
      Assert.Equal(port, expectedPort);
    }

    [Theory]
    [MemberData(nameof(InvalidPorts))]
    public void InvalidPortTests(string input, string expectedDomain, string expectedPort = null)
    {
      var (domain, port) = Listener.SplitDomainAndPort(input);
      Assert.Equal(domain, expectedDomain);
      Assert.Equal(port, expectedPort);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(null, LPort, LPort)]
    [InlineData(null, LNamed, LNamed)]
    [InlineData(IPort, null, IPort)]
    [InlineData(IPort, LPort, IPort)]
    [InlineData(IPort, LNamed, IPort)]
    [InlineData(INamed, null, INamed)]
    [InlineData(INamed, LPort, LPort)]
    [InlineData(INamed, LNamed, INamed)]
    public void PortPreferenceTests(string instancePortOrNamedInstance, string listenerPortOrNamedInstance,
      string expectedResult)
    {
      var preferredPortOrNamedInstance =
        Listener.GetPreferredPort(instancePortOrNamedInstance, listenerPortOrNamedInstance);
      Assert.Equal(preferredPortOrNamedInstance, expectedResult);
    }
  }
}
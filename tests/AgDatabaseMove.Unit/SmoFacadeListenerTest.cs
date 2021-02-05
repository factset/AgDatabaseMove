using System.Collections.Generic;
using Xunit;

namespace AgDatabaseMove.Unit
{
  public class SmoFacadeListenerTest
  {

    /// <summary>
    /// Tests for Listener.SplitDomainAndPort()
    /// </summary>

    private const string DOMAIN = "abc.def.ghi";
    private const string HOST = "abc";

    // {input, expectedDomain, expectedPort (can be null)}
    public static IEnumerable<object[]> ValidPorts => new List<object[]> {
      new object[] { $"{DOMAIN}", DOMAIN },
      new object[] { $"{DOMAIN},123", DOMAIN, ",123" },
      new object[] { $"{DOMAIN}\\SQL", DOMAIN, "\\SQL" },

      new object[] { $"{HOST}", HOST },
      new object[] { $"{HOST},123", HOST, ",123" },
      new object[] { $"{HOST}\\SQL", HOST, "\\SQL" }
    };

    [Theory]
    [MemberData(nameof(ValidPorts))]
    public void ValidPortTests(string input, string expectedDomain, string expectedPort=null)
    {
      var (domain, port) = SmoFacade.Listener.SplitDomainAndPort(input);
      Assert.Equal(domain, expectedDomain);
      Assert.Equal(port, expectedPort);
    }

    // {input, expectedDomain, expectedPort (can be null)}
    public static IEnumerable<object[]> InvalidPorts => new List<object[]> {
      new object[] { $"{DOMAIN}:123", $"{DOMAIN}:123" },
      new object[] { $"{DOMAIN}:SQL", $"{DOMAIN}:SQL" },

      new object[] { $"{HOST}:123", $"{HOST}:123" },
      new object[] { $"{HOST}:SQL", $"{HOST}:SQL" },
    };

    [Theory]
    [MemberData(nameof(InvalidPorts))]
    public void InvalidPortTests(string input, string expectedDomain, string expectedPort=null)
    {
      var (domain, port) = SmoFacade.Listener.SplitDomainAndPort(input);
      Assert.Equal(domain, expectedDomain);
      Assert.Equal(port, expectedPort);
    }


    /// <summary>
    /// Tests for Listener.GetPreferredPort()
    /// </summary>

    private const string IPort = ",123";
    private const string INamed = "\\SQL";

    private const string LPort = ",321";
    private const string LNamed = "\\LQS";
    // { instancePort,  listenerPort,  preferredPort}
    public static IEnumerable<object[]> PortPreferences => new List<object[]> {
      new object[] { null   , null   , null},
      new object[] { null   , LPort  , LPort},
      new object[] { null   , LNamed , LNamed},
      new object[] { IPort  , null   , IPort},
      new object[] { IPort  , LPort  , IPort},
      new object[] { IPort  , LNamed , IPort},
      new object[] { INamed , null   , INamed},
      new object[] { INamed , LPort  , LPort},
      new object[] { INamed , LNamed , INamed},
    };

    [Theory]
    [MemberData(nameof(PortPreferences))]
    public void PortPreferenceTests(string instancePort=null, string listenerPort=null, string preferredPort=null)
    {
      var port = SmoFacade.Listener.GetPreferredPort(instancePort, listenerPort);
      Assert.Equal(port, preferredPort);
    }

  }
}

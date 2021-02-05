using System.Collections.Generic;
using Xunit;

namespace AgDatabaseMove.Unit
{
  public class SmoFacadeListenerTest
  {
    private const string DOMAIN = "abc.def.ghi";
    private const string HOST = "abc";

    // {input, expectedDomain, expectedPort (can be null)}
    public static IEnumerable<object[]> ValidPorts => new List<object[]> {
      new object[] {$"{DOMAIN}", DOMAIN},
      new object[] {$"{DOMAIN},123", DOMAIN, ",123" },
      new object[] {$"{DOMAIN}\\SQL", DOMAIN, "\\SQL" },

      new object[] {$"{HOST}", HOST},
      new object[] {$"{HOST},123", HOST, ",123" },
      new object[] {$"{HOST}\\SQL", HOST, "\\SQL" }
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
      new object[] {$"{DOMAIN}:123", $"{DOMAIN}:123" },
      new object[] {$"{DOMAIN}:SQL", $"{DOMAIN}:SQL" },

      new object[] {$"{HOST}:123", $"{HOST}:123" },
      new object[] {$"{HOST}:SQL", $"{HOST}:SQL" },
    };

    [Theory]
    [MemberData(nameof(InvalidPorts))]
    public void InvalidPortTests(string input, string expectedDomain, string expectedPort=null)
    {
      var (domain, port) = SmoFacade.Listener.SplitDomainAndPort(input);
      Assert.Equal(domain, expectedDomain);
      Assert.Equal(port, expectedPort);
    }
  }
}

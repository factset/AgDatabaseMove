namespace AgDatabaseMove.Integration.Config
{
  using Microsoft.Extensions.Configuration;


  public class TestConfiguration<T>
  {
    public T _config;

    public TestConfiguration(string section)
    {
      var builder = new ConfigurationBuilder()
        .AddJsonFile("config.json", false);

      _config = builder.Build().GetSection(section).Get<T>();
    }
  }
}
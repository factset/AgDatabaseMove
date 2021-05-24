namespace AgDatabaseMove.Cli
{
  using System;
  using System.Text.RegularExpressions;
  using Microsoft.Extensions.Configuration;


  internal class MoveArgs
  {
    public DatabaseConfig From { get; set; }
    public DatabaseConfig To { get; set; }
    public bool Overwrite { get; set; } = false;
    public bool Finalize { get; set; } = true;
    public bool CopyLogins { get; set; } = true;
    public bool DeleteSource { get; set; } = false;
  }


  internal class Program
  {
    private static void Main(string[] args)
    {
      var builder = new ConfigurationBuilder().AddCommandLine(args);
      var arguments = builder.Build().Get<MoveArgs>();

      Console.WriteLine("Beginning AgDatabaseMove...");

      var mover = new AgDatabaseMove(new MoveOptions {
        Source = new AgDatabase(arguments.From),
        Destination = new AgDatabase(arguments.To),
        Overwrite = arguments.Overwrite,
        Finalize = arguments.Finalize,
        CopyLogins = arguments.CopyLogins,
        RetryDuration = attemptNumber => TimeSpan.FromSeconds(10 * attemptNumber),
        FileRelocator = filename =>
          RestoreFileRelocator(arguments.From.DatabaseName, arguments.To.DatabaseName, filename)
      });

      try {
        mover.Move();
      }
      catch(Exception e) {
        Console.WriteLine(e.Message);
        Console.WriteLine(e.StackTrace);

        Console.WriteLine(e.InnerException?.Message);
        Console.WriteLine(e.InnerException?.StackTrace);
        throw;
      }
    }

    private static string RestoreFileRelocator(string fromName, string toName, string fileName)
    {
      // Our integration environment is currently mixed.
      // TODO: If the default file path is used on the source, use the default file path on the destination.
      // SELECT SERVERPROPERTY('InstanceDefaultDataPath') AS InstanceDefaultDataPath, SERVERPROPERTY('InstanceDefaultLogPath') AS InstanceDefaultLogPath
      fileName = Regex.Replace(fileName, "MSSQL11", "MSSQL12", RegexOptions.IgnoreCase & RegexOptions.CultureInvariant);
      fileName = Regex.Replace(fileName, fromName, toName, RegexOptions.IgnoreCase & RegexOptions.CultureInvariant);
      return fileName;
    }
  }
}
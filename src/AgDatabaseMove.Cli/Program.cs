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
        FileRelocator = filename =>
          RestoreFileRelocator(arguments.From.DatabaseName, arguments.To.DatabaseName, filename)
      });

      mover.Move();
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
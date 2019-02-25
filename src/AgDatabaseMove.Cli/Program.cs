namespace AgDatabaseMove.Cli
{
  using System;
  using System.Security.Cryptography;
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
      var builder = new ConfigurationBuilder()
        .AddCommandLine(args);
      var arguments = builder.Build().Get<MoveArgs>();

      var from = new AgDatabase(arguments.From);
      var to = new AgDatabase(arguments.To);

      // TODO: This should be baked into the restore class. We have a property for it.
      if(arguments.Overwrite)
        to.Delete();

      Console.WriteLine("Beginning restore...");
      var restore = new Restore(from, to) {
        CopyLogins = arguments.CopyLogins,
        FileRelocator = fileName => RestoreFileRelocator(from.Name, to.Name, fileName), Finalize = arguments.Finalize,
        Overwrite = arguments.Overwrite
      };
      from.LogBackup();
      restore.AgDbRestore();
      Console.WriteLine("Restore completed.");

      Console.WriteLine("Deleting source.");
      from.Delete();
      Console.WriteLine("Source deleted");
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
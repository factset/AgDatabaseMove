using System.Runtime.CompilerServices;


[assembly: InternalsVisibleTo("AgDatabaseMove.Integration")]
[assembly: InternalsVisibleTo("AgDatabaseMove.Unit")]

namespace AgDatabaseMove
{
  using System;
  using System.Linq;
  using Exceptions;
  using SmoFacade;


  public class MoveOptions
  {
    public IAgDatabase Source { get; set; }
    public IAgDatabase Destination { get; set; }
    public bool Overwrite { get; set; }
    public bool DeleteSource { get; set; } = true;
    public bool Finalize { get; set; }
    public bool CopyLogins { get; set; }
    public Func<string, string> FileRelocator { get; set; }
  }

  /// <summary>
  ///   Used to manage the restore process.
  /// </summary>
  public class AgDatabaseMove
  {
    internal readonly MoveOptions _options;

    public AgDatabaseMove(MoveOptions options)
    {
      _options = options;
    }

    internal LoginProperties UpdateDefaultDb(LoginProperties loginProperties)
    {
      loginProperties.DefaultDatabase =
        _options.Source.Name.Equals(loginProperties.DefaultDatabase, StringComparison.InvariantCultureIgnoreCase)
          ? _options.Destination.Name
          : "master";
      return loginProperties;
    }

    /// <summary>
    ///   AgDatabaseMove the database to all instances of the availability group.
    ///   To join the AG, Finalize must be set.
    /// </summary>
    /// <param name="lastLsn">The last restored LSN used to continue while in no recovery mode.</param>
    /// <returns>The last LSN restored.</returns>
    public decimal Move(decimal? lastLsn = null)
    {
      if(!_options.Overwrite && _options.Destination.Exists() && !_options.Destination.Restoring)
        throw new ArgumentException("Database exists and overwrite option is not set");

      if(lastLsn == null && _options.Destination.Restoring)
        throw new
          ArgumentException("lastLsn parameter can only be used if the Destination database is in a restoring state");

      if(_options.Overwrite)
        _options.Destination.Delete();

      _options.Source.LogBackup();

      var backupChain = new BackupChain(_options.Source);
      var backupList = backupChain.OrderedBackups.ToList();

      if(_options.Destination.Restoring && lastLsn != null)
        backupList.RemoveAll(b => b.LastLsn <= lastLsn.Value);

      if(!backupList.Any())
        throw new BackupChainException("No backups found to restore");

      _options.Destination.Restore(backupList, _options.FileRelocator);

      if(_options.CopyLogins)
        _options.Destination.CopyLogins(_options.Source.AssociatedLogins().Select(UpdateDefaultDb).ToList());

      if(_options.Finalize)
        _options.Destination.JoinAg();

      if(_options.DeleteSource)
        _options.Source.Delete();

      return backupList.Max(bl => bl.LastLsn);
    }
  }
}
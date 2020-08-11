using System.Runtime.CompilerServices;


[assembly: InternalsVisibleTo("AgDatabaseMove.Integration")]
[assembly: InternalsVisibleTo("AgDatabaseMove.Unit")]

namespace AgDatabaseMove
{
  using System;
  using System.Linq;
  using Exceptions;
  using SmoFacade;


  public interface IMoveOptions
  {
    IAgDatabase Source { get; set; }
    IAgDatabase Destination { get; set; }
    bool Overwrite { get; set; }
    bool Finalize { get; set; }
    bool CopyLogins { get; set; }
    Func<string, string> FileRelocator { get; set; }
  }

  public class MoveOptions : IMoveOptions
  {
    public IAgDatabase Source { get; set; }
    public IAgDatabase Destination { get; set; }
    public bool Overwrite { get; set; }
    public bool Finalize { get; set; }
    public bool CopyLogins { get; set; }
    public Func<string, string> FileRelocator { get; set; }
  }

  /// <summary>
  ///   Used to manage the restore process.
  /// </summary>
  public class AgDatabaseMove
  {
    internal readonly IMoveOptions _options;

    public AgDatabaseMove(IMoveOptions options)
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
      if(_options.Overwrite)
        _options.Destination.Delete();

      _options.Source.LogBackup();

      if(!_options.Overwrite && _options.Destination.Exists() && !_options.Destination.Restoring)
        throw new ArgumentException("Database exists and overwrite option is not set.");

      if(lastLsn != null && !_options.Destination.Restoring)
        throw new
          ArgumentException("Database is not in a restoring state which is required to use the lastLsn parameter.");

      var backupChain = new BackupChain(_options.Source);
      var backupList = backupChain.RestoreOrder.ToList();

      if(_options.Destination.Restoring && lastLsn != null) {
        backupList.RemoveAll(b => b.LastLsn <= lastLsn.Value);

        if(!backupList.Any())
          throw new BackupChainException("No backups found to restore.");
      }

      _options.Destination.Restore(backupList, _options.FileRelocator);

      if(_options.Finalize)
        _options.Destination.JoinAg();

      if(_options.CopyLogins)
        _options.Destination.CopyLogins(_options.Source.AssociatedLogins().Select(UpdateDefaultDb).ToList());

      var backupLastLsn = backupList.Max(bl => bl.LastLsn);
      _options.Source.Delete();
      return backupLastLsn;
    }
  }
}
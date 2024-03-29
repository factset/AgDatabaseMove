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
    public bool Finalize { get; set; }
    public bool CopyLogins { get; set; }
    public Func<int, TimeSpan> RetryDuration { get; set; }
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

      if(_options.Overwrite && lastLsn == null)
        _options.Destination.Delete();

      if(lastLsn == null && _options.Destination.Restoring)
        throw new
          ArgumentException("lastLsn parameter can only be used if the Destination database is in a restoring state");

      _options.Source.LogBackup();

      var backupChain = new BackupChain(_options.Source);
      var stripedBackupList = backupChain.OrderedBackups.ToList();

      if(_options.Destination.Restoring && lastLsn != null)
        stripedBackupList.RemoveAll(b => b.LastLsn <= lastLsn.Value);

      if(!stripedBackupList.Any())
        throw new BackupChainException("No backups found to restore");
      
      _options.Destination.Restore(stripedBackupList, _options.RetryDuration, _options.FileRelocator);

      if(_options.CopyLogins)
        foreach(var loginProperty in _options.Source.AssociatedLogins().Select(UpdateDefaultDb))
          _options.Destination.AddLogin(loginProperty);

      if(_options.Finalize) {
        _options.Destination.JoinAg();
      }

      return stripedBackupList.Max(bl => bl.LastLsn);
    }
  }
}
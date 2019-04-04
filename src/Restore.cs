namespace AgDatabaseMove
{
  using System;
  using System.Linq;
  using System.Text.RegularExpressions;
  using Exceptions;
  using SmoFacade;


  /// <summary>
  ///   Used to manage the restore process.
  /// </summary>
  public class Restore
  {
    private readonly IAgDatabase _destination;
    private readonly IAgDatabase _source;

    /// <summary>
    ///   Restores to an AG Database from an existing AgDatabase.
    /// </summary>
    public Restore(IAgDatabase source, IAgDatabase destination)
    {
      _source = source;
      _destination = destination;
      Overwrite = false;
      Finalize = false;
      FileRelocator = name =>
        Regex.Replace(name, _source.Name, _destination.Name, RegexOptions.IgnoreCase & RegexOptions.CultureInvariant);
    }

    /// <summary>
    ///   Option to overwrite a database with the restore.
    /// </summary>
    public bool Overwrite { get; set; }

    /// <summary>
    ///   Option to copy existing logins with SID and password if relevant.
    /// </summary>
    public bool CopyLogins { get; set; }

    /// <summary>
    ///   Option to restore with recovery and join the AG preventing further log restores.
    /// </summary>
    public bool Finalize { get; set; }

    /// <summary>
    ///   Func to be called on each restored file to move the file location.
    /// </summary>
    public Func<string, string> FileRelocator { get; set; }

    internal LoginProperties UpdateDefaultDb(LoginProperties loginProperties)
    {
      loginProperties.DefaultDatabase = _source.Name == loginProperties.DefaultDatabase ? _destination.Name : null;
      return loginProperties;
    }

    /// <summary>
    ///   Restore the database to all instances of the availability group.
    ///   To join the AG, Finalize must be set.
    /// </summary>
    /// <param name="lastLsn">The last restored LSN used to continue while in no recovery mode.</param>
    /// <returns>The last LSN restored.</returns>
    public decimal AgDbRestore(decimal? lastLsn = null)
    {
      if(!Overwrite && _destination.Exists() && !_destination.Restoring)
        throw new ArgumentException("Database exists and overwrite option is not set.");

      if(lastLsn != null && !_destination.Restoring)
        throw new
          ArgumentException("Database is not in a restoring state which is required to use the lastLsn parameter.");

      var backupChain = new BackupChain(_source);
      var backupList = backupChain.RestoreOrder.ToList();

      if(_destination.Restoring && lastLsn != null) {
        backupList.RemoveAll(b => b.LastLsn <= lastLsn.Value);

        if(!backupList.Any())
          throw new BackupChainException("No backups found to restore.");
      }

      _destination.Restore(backupList, FileRelocator);

      if(Finalize)
        _destination.JoinAg();

      if(CopyLogins) _destination.CopyLogins(_source.AssociatedLogins().Select(UpdateDefaultDb).ToList());

      return backupList.Max(bl => bl.LastLsn);
    }
  }
}
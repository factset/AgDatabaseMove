using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace AgDatabaseMove
{

  public class StripedBackupSet
  {
    public IEnumerable<BackupMetadata> StripedBackups { get; private set; }

    private StripedBackupSet(IEnumerable<BackupMetadata> stripedBackups)
    {
      StripedBackups = stripedBackups;
    }

    public static IEnumerable<StripedBackupSet> GetStripedBackupSetChain(IEnumerable<BackupMetadata> backups)
    {
      var chain = backups
        .GroupBy(b => new
        {
          b.FirstLsn,
          b.LastLsn,
          b.CheckpointLsn,
          b.DatabaseBackupLsn,
          b.BackupType,
          b.DatabaseName
        })
        .Select(group => new StripedBackupSet(group.ToList()));
      return chain;
    }
  }
}

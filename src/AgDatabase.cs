using System.Runtime.CompilerServices;


[assembly: InternalsVisibleTo("AgDatabaseMove.Integration")]
[assembly: InternalsVisibleTo("AgDatabaseMove.Unit")]

namespace AgDatabaseMove
{
  using System;
  using System.Collections.Concurrent;
  using System.Collections.Generic;
  using System.Data.SqlClient;
  using System.Linq;
  using System.Threading;
  using System.Threading.Tasks;
  using Exceptions;
  using Microsoft.SqlServer.Management.Smo;
  using Polly;
  using SmoFacade;
  using AvailabilityGroup = SmoFacade.AvailabilityGroup;
  using Server = SmoFacade.Server;


  public interface IAgDatabase
  {
    IListener Listener { get; }
    bool Restoring { get; }
    string Name { get; }
    bool Exists();
    void Delete();
    void LogBackup();
    List<SingleBackup> RecentBackups();
    void JoinAg();

    void Restore(IEnumerable<StripedBackup> stripedBackupChain, Func<int, TimeSpan> retryDurationProvider,
      Func<string, string> fileRelocation = null);

    void Restore(SingleBackup fullBackup, Func<int, TimeSpan> retryDurationProvider,
      Func<string, string> fileRelocation = null);

    void RenameLogicalFileName(Func<string, string> fileRenamer);
    void AddLogin(LoginProperties login);
    IEnumerable<LoginProperties> AssociatedLogins();
    void DropLogin(LoginProperties login);
    void DropAllLogins();
    void AddUser(UserProperties user);
    void DropUser(UserProperties user);
    void AddRole(LoginProperties login, RoleProperties role);
    IEnumerable<RoleProperties> AssociatedRoles();
    void ContainsLogin(string loginName);
    void SetSizeLimit(int maxMB);
    void SetGrowthRate(int growthMB);
    void SetLogGrowthRate(int growthMB);
  }


  /// <summary>
  ///   A connection to the primary instance of an availability group referencing a database name.
  ///   The database does not have to exist or be a part of the availability group, and can be created or added to the AG via
  ///   this interface.
  /// </summary>
  public class AgDatabase : IDisposable, IAgDatabase
  {
    private readonly string _backupPathSqlQuery;

    /// <summary>
    ///   A constructor that uses a config object for more options.
    /// </summary>
    /// <param name="dbConfig">A DatabaseConfig where the DataSource is the AG listener.</param>
    public AgDatabase(DatabaseConfig dbConfig)
    {
      Name = dbConfig.DatabaseName;
      _backupPathSqlQuery = dbConfig.BackupPathSqlQuery;
      Listener = new Listener(new SqlConnectionStringBuilder(dbConfig.ConnectionString) { InitialCatalog = "master" },
                               dbConfig.CredentialName);
    }

    public decimal SizeMb => Listener.Primary.DatabaseSizeMb(Name);

    public int ServerRemainingDiskMb => Listener.Primary.RemainingDiskMb();

    public IListener Listener { get; }

    /// <summary>
    ///   Determines if the database is in a restoring state.
    /// </summary>
    public bool Restoring => Listener.Primary.Database(Name)?.Restoring ?? false;

    /// <summary>
    ///   Database name
    /// </summary>
    public string Name { get; }

    /// <summary>
    ///   Determines if the database exists.
    /// </summary>
    public bool Exists()
    {
      return Listener.Primary.Database(Name) != null;
    }

    /// <summary>
    ///   Removes the database from the AG and deletes it from all instances.
    /// </summary>
    public void Delete()
    {
      Listener.ForEachAgInstance((s, ag) => {
        if (!ag.IsPrimaryInstance)
        {
          ag.Remove(Name);
          s.Database(Name)?.Drop();
        }
      });
      Listener.AvailabilityGroup.Remove(Name);
      Listener.Primary.Database(Name)?.Drop();
    }

    /// <summary>
    ///   Takes a log backup on the primary instance.
    /// </summary>
    public void LogBackup()
    {
      Listener.Primary.LogBackup(Name, _backupPathSqlQuery);
    }

    /// <summary>
    ///   Restores the database backups to each instance in the AG.
    ///   We suggest using <see cref="AgDatabaseMove" /> to assist with restores.
    /// </summary>
    /// <param name="backupOrder">An ordered list of backups to restore.</param>
    /// <param name="retryDurationProvider">Retry duration function.</param>
    /// <param name="fileRelocation">A method to generate the new file location when moving the database.</param>
    public void Restore(IEnumerable<StripedBackup> stripedBackupChain, Func<int, TimeSpan> retryDurationProvider,
      Func<string, string> fileRelocation = null)
    {
      Listener.ForEachAgInstance(s => s.Restore(stripedBackupChain, Name, retryDurationProvider, fileRelocation));
    }

    public void Restore(SingleBackup fullBackup, Func<int, TimeSpan> retryDurationProvider,
      Func<string, string> fileRelocation = null)
    {
      if(fullBackup.BackupType != BackupFileTools.BackupType.Full)
        throw new ArgumentException("Provided backup must be a full database backup.");
      Listener.ForEachAgInstance(s => s.Restore(fullBackup, Name, retryDurationProvider, fileRelocation));
    }

    public void RenameLogicalFileName(Func<string, string> fileRenamer)
    {
      Listener.Primary.RenameLogicalFileName(Name, fileRenamer);
    }

    /// <summary>
    ///   Builds a list of recent backups from msdb on each AG instance.
    /// </summary>
    public List<SingleBackup> RecentBackups()
    {
      // find most recent full backup LSN across all replica servers
      var fullBackupLsnBag = new ConcurrentBag<decimal>();
      Listener.ForEachAgInstance(s => 
      {
        var lsn = s.Database(Name).MostRecentFullBackupLsn();
        if (lsn != null)
          fullBackupLsnBag.Add(lsn.Value);
      });

      // find all backups in that chain
      if (fullBackupLsnBag.IsEmpty)
        throw new Exception($"Could not find any full backups for DB '{Name}'");

      var databaseBackupLsn = fullBackupLsnBag.Max();
      var bag = new ConcurrentBag<SingleBackup>();
      Listener.ForEachAgInstance(s => s.Database(Name).BackupChainFromLsn(databaseBackupLsn)
                                    .ForEach(backup => bag.Add(backup)));
      return bag.ToList();
    }

    /// <summary>
    ///   Joins the database to the AG on each instance.
    /// </summary>
    public void JoinAg()
    {
      FinalizePrimary();
      Listener.ForEachAgInstance((s, ag) => {
        if(ag.IsPrimaryInstance)
          ag.JoinPrimary(Name);
      });
      Listener.ForEachAgInstance((s, ag) => {
        if(!ag.IsPrimaryInstance)
          ag.JoinSecondary(Name);
      });
    }

    public IEnumerable<LoginProperties> AssociatedLogins()
    {
      return Listener.Primary.Database(Name).Users.Where(u => u.Login != null && u.Login.Name != "sa")
        .Select(u => u.Login.Properties());
    }

    public void DropLogin(LoginProperties login)
    {
      Listener.ForEachAgInstance(server => server.DropLogin(login));
    }

    public void DropAllLogins()
    {
      Listener.ForEachAgInstance(s => s.Database(Name)?.DropAssociatedLogins());
    }

    public void AddLogin(LoginProperties login)
    {
      if (login.LoginType == LoginType.SqlLogin && login.Sid == null) {
        AddNewSqlLogin(login);
      } 
      else {
        Listener.ForEachAgInstance(server => server.AddLogin(login));
      }
    }

    private void AddNewSqlLogin(LoginProperties login)
    {
      var createdLogin = Listener.Primary.AddLogin(login);
      login.Sid = createdLogin.Sid;
      Parallel.ForEach(Listener.Secondaries, server => server.AddLogin(login));
    }

    public void AddUser(UserProperties user)
    {
      Listener.Primary.Database(Name).AddUser(user);
    }

    public void DropUser(UserProperties user)
    {
      Listener.Primary.Database(Name)?.DropUser(user);
    }

    public IEnumerable<RoleProperties> AssociatedRoles()
    {
      return Listener.Primary.Roles.Select(r => r.Properties());
    }

    public void AddRole(LoginProperties login, RoleProperties role)
    {
      Listener.ForEachAgInstance(server => server.AddRole(login, role));
    }

    public void ContainsLogin(string loginName)
    {
      var exceptions = new ConcurrentQueue<Exception>();

      Listener.ForEachAgInstance((s, ag) => {
        try {
          CheckLoginExists(s, ag, loginName);
        }
        catch(MissingLoginException ex) {
          exceptions.Enqueue(ex);
        }
        catch(MultipleLoginException ex) {
          exceptions.Enqueue(ex);
        }
        catch(MissingSidException ex) {
          exceptions.Enqueue(ex);
        }
      });

      if(exceptions.Count > 0) throw new AggregateException(exceptions);
    }

    /// <summary>
    ///   IDisposable implemented for our connection to the primary AG database server.
    /// </summary>
    public void Dispose()
    {
      Listener?.Dispose();
    }

    public void FullBackup()
    {
      Listener.Primary.FullBackup(Name, _backupPathSqlQuery);
    }

    public void FinalizePrimary()
    {
      Listener.ForEachAgInstance(FinalizePrimary);
    }

    private void FinalizePrimary(Server server, AvailabilityGroup availabilityGroup)
    {
      if(!availabilityGroup.IsPrimaryInstance)
        return;

      var database = server.Database(Name);
      if(!database.Restoring)
        return;

      database.RestoreWithRecovery();
    }

    public bool IsInitializing()
    {
      var result = 0;
      Listener.ForEachAgInstance((s, ag) => {
        if(ag.IsInitializing(Name))
          Interlocked.Increment(ref result);
      });
      return result > 0;
    }

    public void RestrictedUserMode()
    {
      Listener.Primary.Database(Name).RestrictedUserMode();
    }

    public void MultiUserMode()
    {
      Listener.Primary.Database(Name).MultiUserMode();
    }

    public void SetSizeLimit(int maxMB)
    {
      Listener.Primary.Database(Name).SetSizeLimit(maxMB);
    }

    public void SetGrowthRate(int growthMB)
    {
      Listener.Primary.Database(Name).SetGrowthRate(growthMB);
    }

    public void SetLogGrowthRate(int growthMB)
    {
      Listener.Primary.Database(Name).SetLogGrowthRate(growthMB);
    }

    public void CheckDBConnections(int connectionTimeout)
    {
      Listener.ForEachAgInstance(server => server.CheckDBConnection(Name, connectionTimeout));
    }

    private void CheckLoginExists(Server server, AvailabilityGroup availabilityGroup, string loginName)
    {
      var matchingLogins = server.Logins.Where(l => l.Name == loginName);

      if(matchingLogins.Count() == 0)
        throw new
          MissingLoginException($"Login missing on {server.Name}, {Listener.AvailabilityGroup.Name}, {loginName}");

      if(matchingLogins.Count() > 1)
        throw new
          MultipleLoginException($"Multiple logins exist on {server.Name}, {Listener.AvailabilityGroup.Name}, {loginName}");

      var sid = matchingLogins.First().Sid;
      if(sid == null || sid.Length == 0)
        throw new MissingSidException($"Sid missing on {server.Name}, {Listener.AvailabilityGroup.Name}, {loginName}");
    }
  }
}
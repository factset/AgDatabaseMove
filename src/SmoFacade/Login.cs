namespace AgDatabaseMove.SmoFacade
{
  using System;
  using System.Collections.Generic;
  using System.Data.SqlClient;
  using System.Linq;
  using Microsoft.SqlServer.Management.Smo;


  public class LoginProperties
  {
    public string Name { get; set; }
    public LoginType LoginType { get; set; }
    public byte[] Sid { get; set; }
    public string PasswordHash { get; set; }
    public string Password { get; set; }
    public string DefaultDatabase { get; set; }
  }


  public class Login
  {
    private readonly Microsoft.SqlServer.Management.Smo.Login _login;
    private readonly Server _server;

    public Login(Microsoft.SqlServer.Management.Smo.Login login, Server server)
    {
      _login = login;
      _server = server;
    }

    /// <summary>
    ///   Creates a new login on the server.
    /// </summary>
    /// <param name="loginProperties">Values for login initialization</param>
    /// <param name="server">The server to create the login on</param>
    public Login(LoginProperties loginProperties, Server server)
    {
      _server = server;
      _login = ConstructLogin(loginProperties, server);
    }

    public static IEqualityComparer<Login> Comparer { get; } = new LoginEqualityComparer();

    public string Name => _login.Name;
    public byte[] Sid => _login.Sid;
    private LoginType LoginType => _login.LoginType;
    private string DefaultDatabase => _login.DefaultDatabase;


    /// <summary>
    ///   Queries the server for the login's password hash.
    /// </summary>
    /// <returns>A string containing the hex representation of the password hash.</returns>
    public string PasswordHash()
    {
      var sql = "SELECT CAST(password AS varbinary(max)) as passwordHash FROM sys.syslogins WHERE name = @loginName";

      var cmd = new SqlCommand(sql, _server.SqlConnection);
      var dbName = cmd.CreateParameter();
      dbName.ParameterName = "loginName";
      dbName.Value = Name;
      cmd.Parameters.Add(dbName);

      using var reader = cmd.ExecuteReader();
      if(!reader.Read() || reader.IsDBNull(reader.GetOrdinal("passwordHash")))
        return null;
      return HashString((byte[])reader["passwordHash"]);
    }

    private string HashString(byte[] passwordHash)
    {
      return "0x" + BitConverter.ToString(passwordHash).Replace("-", string.Empty);
    }

    /// <summary>
    ///   Copy the login properties up so we can access them across threads.
    ///   Accessing the properties on the underlying login object is not thread safe.
    /// </summary>
    /// <returns></returns>
    public LoginProperties Properties()
    {
      return new LoginProperties {
        LoginType = LoginType,
        Name = Name,
        PasswordHash = LoginType == LoginType.SqlLogin ? PasswordHash() : null,
        Sid = Sid,
        DefaultDatabase = DefaultDatabase
      };
    }

    public void Drop()
    {
      _login.Drop();
    }

    private static Microsoft.SqlServer.Management.Smo.Login ConstructLogin(LoginProperties loginProperties,
      Server server)
    {
      var login = new Microsoft.SqlServer.Management.Smo.Login(server._server, loginProperties.Name) {
        LoginType = loginProperties.LoginType,
        Sid = loginProperties.Sid,
        DefaultDatabase = loginProperties.DefaultDatabase
      };

      if(loginProperties.LoginType == LoginType.SqlLogin) {
        if(loginProperties.PasswordHash != null)
          login.Create(loginProperties.PasswordHash, LoginCreateOptions.IsHashed);
        else if(loginProperties.Password != null)
          login.Create(loginProperties.Password);
        else
          throw new ArgumentException("Password or hash was not supplied for sql login.");
      }
      else {
        login.Create();
      }

      return login;
    }

    /// <summary>
    ///   TODO: standardize the style for this and the one in BackupMetadata
    /// </summary>
    private sealed class LoginEqualityComparer : IEqualityComparer<Login>
    {
      public bool Equals(Login x, Login y)
      {
        if(ReferenceEquals(x, y))
          return true;
        if(ReferenceEquals(x, null))
          return false;
        if(ReferenceEquals(y, null))
          return false;
        if(x.GetType() != y.GetType())
          return false;
        return x.Name.Equals(y.Name, StringComparison.InvariantCultureIgnoreCase) && x.Sid.SequenceEqual(y.Sid);
      }

      public int GetHashCode(Login obj)
      {
        unchecked {
          var hashCode = obj.Name.GetHashCode();
          hashCode = (hashCode * 397) ^ obj.Sid.GetHashCode();
          return hashCode;
        }
      }
    }
  }
}
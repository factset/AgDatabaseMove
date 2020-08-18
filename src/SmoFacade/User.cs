namespace AgDatabaseMove.SmoFacade
{
  public class User
  {
    private readonly Server _server;
    private readonly Microsoft.SqlServer.Management.Smo.User _user;

    public User(Microsoft.SqlServer.Management.Smo.User user, Server server)
    {
      _user = user;
      _server = server;
    }

    public string Name => _user.Name;

    public Login Login
    {
      get
      {
        var login = _user.Parent.Parent.Logins[_user.Login];
        if(login == null)
          return null;
        return new Login(login, _server);
      }
    }
  }
}
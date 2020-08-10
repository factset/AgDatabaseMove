namespace AgDatabaseMove.SmoFacade
{
  using Smo = Microsoft.SqlServer.Management.Smo;


  public class User
  {
    private readonly Server _server;
    private readonly Smo.User _user;

    public User(Smo.User user, Server server)
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

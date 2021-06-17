namespace AgDatabaseMove.SmoFacade
{
  using Microsoft.SqlServer.Management.Smo;


  public class Role
  {
    private readonly ServerRole _role;
    private readonly Server _server;

    public Role(ServerRole role, Server server)
    {
      _role = role;
      _server = server;
    }

    public string Name => _role.Name;
  }
}
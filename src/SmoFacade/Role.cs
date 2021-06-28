namespace AgDatabaseMove.SmoFacade
{
  using Microsoft.SqlServer.Management.Smo;


  public class RoleProperties
  {
    public string Name { get; set; }
  }

  public class Role
  {
    private readonly ServerRole _role;
    private readonly Server _server;

    public Role(ServerRole role, Server server)
    {
      _role = role;
      _server = server;
    }

    public RoleProperties Properties()
    {
      return new RoleProperties {
        Name = _role.Name
      };
    }
  }
}
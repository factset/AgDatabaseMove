﻿namespace AgDatabaseMove.SmoFacade
{
  using System.Collections.Generic;
  using Smo = Microsoft.SqlServer.Management.Smo;

  public class UserProperties
  {
    public string Name { get; set; }
    public IEnumerable<RoleProperties> Roles { get; set; }
    public Smo.DatabasePermissionSet Permissions { get; set; }
    public string LoginName { get; set; }
  }

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

    public void AddRole(RoleProperties role)
    {
      _user.AddToRole(role.Name);
    }

    public void Drop()
    {
      _user.Drop();
    }

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
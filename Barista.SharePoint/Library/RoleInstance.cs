﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Barista.SharePoint.Library
{
  using Barista.DocumentStore;
  using Jurassic;
  using Jurassic.Library;
  using System;

  public class RoleInstance : ObjectInstance
  {
    Role m_role;

    public RoleInstance(ScriptEngine engine, Role role)
      : base(engine)
    {
      if (role == null)
        throw new ArgumentNullException("role");

      m_role = role;

      this.PopulateFields();
      this.PopulateFunctions();
    }

    [JSProperty(Name = "name")]
    public string Name
    {
      get { return m_role.Name; }
      set { m_role.Name = value; }
    }

    [JSProperty(Name = "description")]
    public string Description
    {
      get { return m_role.Description; }
      set { m_role.Description = value; }
    }

    [JSProperty(Name = "order")]
    public int Order
    {
      get { return m_role.Order; }
      set { m_role.Order = value; }
    }

    [JSProperty(Name = "basePermissions")]
    public ArrayInstance BasePermissions
    {
      get
      {
        var result = this.Engine.Array.Construct(m_role.BasePermissions.Select(bp => bp).ToArray());
        return result;
      }
      set
      {
        m_role.BasePermissions = value.ElementValues.OfType<string>().ToList();
      }
    }
  }
}
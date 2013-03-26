﻿namespace Barista.SharePoint.Library
{
  using System;
  using Jurassic;
  using Jurassic.Library;
  using Barista.DirectoryServices;

  [Serializable]
  public class ActiveDirectoryInstance : ObjectInstance
  {
    public ActiveDirectoryInstance(ScriptEngine engine)
      : base(engine)
    {
      this.PopulateFields();
      this.PopulateFunctions();
    }

    internal string CurrentUserLoginName
    {
      get;
      set;
    }

    [JSProperty(Name = "currentDomain")]
    public string CurrentDomain
    {
      get { return Utilities.GetFarmKeyValue("WindowsDomainShortName"); }
    }

    [JSFunction(Name = "getADUser")]
    public ADUserInstance GetADUser()
    {
      var user = ADHelper.GetADUser(CurrentUserLoginName);

      if (user == null)
        throw new InvalidOperationException("The current user is not an AD user: " + CurrentUserLoginName);

      return new ADUserInstance(this.Engine.Object.InstancePrototype, user);
    }

    public ADUserInstance GetADUser(string loginName)
    {
      if (String.IsNullOrEmpty(loginName))
        loginName = CurrentUserLoginName;

      var user = ADHelper.GetADUser(loginName);

      if (user == null)
        throw new InvalidOperationException("The specified login name did not correspond to a AD user account.");

      return new ADUserInstance(this.Engine.Object.InstancePrototype, user);
    }

    [JSFunction(Name = "getADGroup")]
    public ADGroupInstance GetADGroup(string groupName)
    {
      var group = ADHelper.GetADGroup(groupName);

      return new ADGroupInstance(this.Engine.Object.InstancePrototype, group);
    }

    [JSFunction(Name = "searchAllDirectoryEntries")]
    public ArrayInstance SearchAllDirectoryEntities(string searchText, int maxResults, string principalType)
    {
      var principalTypeEnum = PrincipalType.All;
      if (String.IsNullOrEmpty(principalType) == false)
        principalTypeEnum = (PrincipalType)Enum.Parse(typeof(PrincipalType), principalType);

      var entities = ADHelper.SearchAllDirectoryEntities(searchText, maxResults, principalTypeEnum);

      var result = this.Engine.Array.Construct();

      foreach (var entity in entities)
      {
        if (entity is ADGroup)
        {
          ArrayInstance.Push(result, new ADGroupInstance(this.Engine.Object.InstancePrototype, entity as ADGroup)); 
        }
        else if (entity is ADUser)
        {
          ArrayInstance.Push(result, new ADUserInstance(this.Engine.Object.InstancePrototype, entity as ADUser)); 
        }
      }

      return result;
    }

    [JSFunction(Name = "searchAllGroups")]
    public ArrayInstance SearchAllGroups(string searchText, int maxResults)
    {
      var groups = ADHelper.SearchAllGroups(searchText, maxResults);

      var result = this.Engine.Array.Construct();
      foreach (var group in groups)
      {
        ArrayInstance.Push(result, new ADGroupInstance(this.Engine.Object.InstancePrototype, group)); 
      }
      return result;
    }

    [JSFunction(Name = "searchAllUsers")]
    public ArrayInstance SearchAllUsers(string searchText, int maxResults)
    {
      var users = ADHelper.SearchAllUsers(searchText, maxResults);

      var result = this.Engine.Array.Construct();
      foreach (var user in users)
      {
        ArrayInstance.Push(result, new ADUserInstance(this.Engine.Object.InstancePrototype, user));
      }
      return result;
    }

  }
}
﻿namespace Barista.SharePoint.DocumentStore
{
  using System;
  using System.Security.AccessControl;
  using System.Security.Principal;
  using System.Threading;

  public static class SPEntityMutexManager
  {
    public static Mutex GrabMutex(string documentStoreUrl, Guid entityId)
    {
      var mutexName = "BaristaEntityMutex_" + documentStoreUrl + entityId;
      try
      {
        return Mutex.OpenExisting(mutexName);
      }
      catch (WaitHandleCannotBeOpenedException)
      {
        var worldSid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
        var security = new MutexSecurity();
        var rule = new MutexAccessRule(worldSid, MutexRights.FullControl, AccessControlType.Allow);
        security.AddAccessRule(rule);
        bool mutexIsNew;
        return new Mutex(false, mutexName, out mutexIsNew, security);
      }
      catch (UnauthorizedAccessException)
      {
        var m = Mutex.OpenExisting(mutexName, MutexRights.ReadPermissions | MutexRights.ChangePermissions);
        var security = m.GetAccessControl();
        var user = Environment.UserDomainName + "\\" + Environment.UserName;
        var rule = new MutexAccessRule(user, MutexRights.Synchronize | MutexRights.Modify, AccessControlType.Allow);
        security.AddAccessRule(rule);
        m.SetAccessControl(security);

        return Mutex.OpenExisting(mutexName);
      }
    }
  }
}

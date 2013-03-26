﻿namespace Barista.SharePoint.Bundles
{
  using Barista.Library;
  using Barista.SharePoint.Library;
  using Jurassic;
  using System;

  [Serializable]
  public class WebBundle : IBundle
  {
    public string BundleName
    {
      get { return "Web"; }
    }

    public string BundleDescription
    {
      get { return "Web Bundle. Provides a mechanism to make Ajax calls and query the request and manipulate response of the current context."; } 
    }

    public WebInstance WebInstance
    {
      get;
      private set;
    }

    public object InstallBundle(ScriptEngine engine)
    {
      engine.SetGlobalValue("AjaxSettings", new AjaxSettingsConstructor(engine));
      engine.SetGlobalValue("ProxySettings", new ProxySettingsConstructor(engine));
      engine.SetGlobalValue("Cookie", new CookieConstructor(engine));

      return this.WebInstance ?? (this.WebInstance = new WebInstance(engine));
    }
  }
}
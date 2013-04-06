﻿namespace Barista.DocumentStore
{
  public class Script
  {
    public Script()
    {
      this.Type = "text/javascript";
      this.Language = "JavaScript";
    }

    public string Name
    {
      get;
      set;
    }

    public string Description
    {
      get;
      set;
    }

    public string Type
    {
      get;
      set;
    }

    public string Language
    {
      get;
      set;
    }

    public string Code
    {
      get;
      set;
    }
  }
}

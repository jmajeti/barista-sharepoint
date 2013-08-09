﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Barista.SharePoint.Migration.Library
{
  using Barista.Jurassic;
  using Barista.Jurassic.Library;
  using Microsoft.SharePoint.Deployment;
  using System;

  [Serializable]
  public class SPExportConstructor : ClrFunction
  {
    public SPExportConstructor(ScriptEngine engine)
      : base(engine.Function.InstancePrototype, "SPExport", new SPExportInstance(engine.Object.InstancePrototype))
    {
    }

    [JSConstructorFunction]
    public SPExportInstance Construct()
    {
      return new SPExportInstance(this.InstancePrototype);
    }
  }

  [Serializable]
  public class SPExportInstance : ObjectInstance
  {
    private readonly SPExport m_export;

    public SPExportInstance(ObjectInstance prototype)
      : base(prototype)
    {
      this.PopulateFields();
      this.PopulateFunctions();
    }

    public SPExportInstance(ObjectInstance prototype, SPExport export)
      : this(prototype)
    {
      if (export == null)
        throw new ArgumentNullException("export");

      m_export = export;
    }

    public SPExport SPExport
    {
      get { return m_export; }
    }

    [JSProperty(Name = "settings")]
    public object Settings
    {
      get
      {
        return new SPExportSettingsInstance(this.Engine.Object.InstancePrototype, m_export.Settings);
      }
      set
      {
      }
    }

    [JSFunction(Name = "cancel")]
    public void Cancel()
    {
      m_export.Cancel();
    }

    [JSFunction(Name = "cleanUpAutoGeneratedDataFiles")]
    public void CleanUpAutoGeneratedDataFiles()
    {
      m_export.CleanUpAutoGeneratedDataFiles();
    }

    [JSFunction(Name = "dispose")]
    public void Dispose()
    {
      m_export.Dispose();
    }

    [JSFunction(Name = "run")]
    public void Run()
    {
      m_export.Run();
    }
  }
}
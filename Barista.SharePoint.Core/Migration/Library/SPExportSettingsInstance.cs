﻿namespace Barista.SharePoint.Migration.Library
{
    using Barista.Extensions;
    using Barista.Jurassic;
    using Barista.Jurassic.Library;
    using System;
    using Barista.Newtonsoft.Json;
    using Barista.SharePoint.Library;
    using Microsoft.SharePoint.Deployment;

    [Serializable]
    public class SPExportSettingsConstructor : ClrFunction
    {
        public SPExportSettingsConstructor(ScriptEngine engine)
            : base(engine.Function.InstancePrototype, "SPExportSettings", new SPExportSettingsInstance(engine.Object.InstancePrototype))
        {
            this.PopulateFunctions();
        }

        [JSConstructorFunction]
        public SPExportSettingsInstance Construct()
        {
            return new SPExportSettingsInstance(this.InstancePrototype);
        }

        [JSProperty(Name = "fileExtension")]
        public string FileExtension
        {
            get
            {
                return SPDeploymentSettings.FileExtension;
            }
        }
    }

    [Serializable]
    public class SPExportSettingsInstance : ObjectInstance
    {
        private readonly SPExportSettings m_exportSettings;

        public SPExportSettingsInstance(ObjectInstance prototype)
            : base(prototype)
        {
            m_exportSettings = new SPExportSettings();

            this.PopulateFields();
            this.PopulateFunctions();
        }

        public SPExportSettingsInstance(ObjectInstance prototype, SPExportSettings exportSettings)
            : this(prototype)
        {
            if (exportSettings == null)
                throw new ArgumentNullException("exportSettings");

            m_exportSettings = exportSettings;
        }

        public SPExportSettings SPExportSettings
        {
            get { return m_exportSettings; }
        }

        [JSProperty(Name = "autoGenerateDataFileName")]
        [JsonProperty("autoGenerateDataFileName")]
        [JSDoc("Gets or sets a value that indicates whether a file name for the content migration package should be automatically generated.")]
        public bool AutoGenerateDataFileName
        {
            get
            {
                return m_exportSettings.AutoGenerateDataFileName;
            }
            set
            {
                m_exportSettings.AutoGenerateDataFileName = value;
            }
        }

        [JSProperty(Name = "baseFileName")]
        [JsonProperty("baseFileName")]
        [JSDoc("Gets or sets the base file name used when creating content migration packages.")]
        public string BaseFileName
        {
            get
            {
                return m_exportSettings.BaseFileName;
            }
            set
            {
                m_exportSettings.BaseFileName = value;
            }
        }

        [JSProperty(Name = "commandLineVerbose")]
        [JsonProperty("commandLineVerbose")]
        [JSDoc("Gets or sets a value that determines whether output is written to a command line console.")]
        public bool CommandLineVerbose
        {
            get
            {
                return m_exportSettings.CommandLineVerbose;
            }
            set
            {
                m_exportSettings.CommandLineVerbose = value;
            }
        }

        [JSProperty(Name = "currentChangeToken")]
        [JsonProperty("currentChangeToken")]
        [JSDoc("Gets the current change token in the change log that is used as a comparison value when running subsequent incremental export operations.")]
        public string CurrentChangeToken
        {
            get
            {
                return m_exportSettings.CurrentChangeToken;
            }
        }

        //TODO: DataFiles??

        [JSProperty(Name = "excludeDependencies")]
        [JsonProperty("excludeDependencies")]
        [JSDoc("Specifies whether to exclude dependencies from the export package when exporting objects of type SPFile or SPListItem.")]
        public bool ExcludeDependencies
        {
            get
            {
                return m_exportSettings.ExcludeDependencies;
            }
            set
            {
                m_exportSettings.ExcludeDependencies = value;
            }
        }

        [JSProperty(Name = "exportChangeToken")]
        [JsonProperty("exportChangeToken")]
        [JSDoc("Gets or sets the change token to use when exporting incremental changes based on changes since the last export.")]
        public string ExportChangeToken
        {
            get
            {
                return m_exportSettings.ExportChangeToken;
            }
            set
            {
                m_exportSettings.ExportChangeToken = value;
            }
        }

        [JSProperty(Name = "exportFrontEndFileStreams")]
        [JsonProperty("exportFrontEndFileStreams")]
        [JSDoc("Specifies whether the front end file streams will be exported or not. Default is true.")]
        public bool ExportFrontEndFileStreams
        {
            get
            {
                return m_exportSettings.ExportFrontEndFileStreams;
            }
            set
            {
                m_exportSettings.ExportFrontEndFileStreams = value;
            }
        }

        [JSProperty(Name = "exportMethod")]
        [JsonProperty("exportMethod")]
        [JSDoc("Gets or sets a value that specifies whether you want to do a full or incremental export. Possible Values: ExportAll, ExportChanges.")]
        public string ExportMethod
        {
            get
            {
                return m_exportSettings.ExportMethod.ToString();
            }
            set
            {
                SPExportMethodType exportMethod;
                if (value.TryParseEnum(true, out exportMethod))
                    m_exportSettings.ExportMethod = exportMethod;
            }
        }

        [JSProperty(Name = "exportObjects")]
        [JSDoc("Gets the collection of objects to export.")]
        public SPExportObjectCollectionInstance ExportObjects
        {
            get
            {
                return new SPExportObjectCollectionInstance(this.Engine.Object.InstancePrototype, m_exportSettings.ExportObjects);
            }
        }

        [JSProperty(Name = "exportPublicSchema")]
        [JsonProperty("exportPublicSchema")]
        [JSDoc("Specifies whether the front end file streams will be exported or not. Default is true.")]
        public bool ExportPublicSchema
        {
            get
            {
                return m_exportSettings.ExportPublicSchema;
            }
            set
            {
                m_exportSettings.ExportPublicSchema = value;
            }
        }

        [JSProperty(Name = "fileCompression")]
        [JsonProperty("fileCompression")]
        [JSDoc("Gets or sets a Boolean value that specifies whether the content migration package is compressed using the CAB compression protocol.")]
        public bool FileCompression
        {
            get
            {
                return m_exportSettings.FileCompression;
            }
            set
            {
                m_exportSettings.FileCompression = value;
            }
        }

        [JSProperty(Name = "fileExtension")]
        [JsonIgnore]
        public string FileExtension
        {
            get
            {
                return Microsoft.SharePoint.Deployment.SPDeploymentSettings.FileExtension;
            }
        }

        [JSProperty(Name = "fileLocation")]
        [JsonProperty("fileLocation")]
        [JSDoc(@"Gets or sets the directory location where content migration packages are placed. This value can be any valid URI, for example, http://www.MySite.com/ or \\MySite\.")]
        public string FileLocation
        {
            get
            {
                return m_exportSettings.FileLocation;
            }
            set
            {
                m_exportSettings.FileLocation = value;
            }
        }

        [JSProperty(Name = "fileMaxSize")]
        [JsonProperty("fileMaxSize")]
        [JSDoc("Gets or sets a value that specifies the maximum size for a content migration package (.cmp) file that is outputted by the export operation. By default, the .cmp files are limited to 24 MB in size.")]
        public int FileMaxSize
        {
            get
            {
                return m_exportSettings.FileMaxSize;
            }
            set
            {
                m_exportSettings.FileMaxSize = value;
            }
        }

        [JSProperty(Name = "haltOnNonfatalError")]
        [JsonProperty("haltOnNonfatalError")]
        public bool HaltOnNonfatalError
        {
            get
            {
                return m_exportSettings.HaltOnNonfatalError;
            }
            set
            {
                m_exportSettings.HaltOnNonfatalError = value;
            }
        }

        [JSProperty(Name = "haltOnWarning")]
        [JsonProperty("haltOnWarning")]
        public bool HaltOnWarning
        {
            get
            {
                return m_exportSettings.HaltOnWarning;
            }
            set
            {
                m_exportSettings.HaltOnWarning = value;
            }
        }

        [JSProperty(Name = "includeSecurity")]
        [JsonProperty("includeSecurity")]
        [JSDoc("Gets or sets a value that determines whether site security groups and the group membership information is exported or imported. Possible Values: All, None, WssOnly.")]
        public string IncludeSecurity
        {
            get
            {
                return m_exportSettings.IncludeSecurity.ToString();
            }
            set
            {
                SPIncludeSecurity includeSecurity;
                if (value.TryParseEnum(true, out includeSecurity))
                    m_exportSettings.IncludeSecurity = includeSecurity;
            }
        }

        [JSProperty(Name = "includeVersions")]
        [JsonProperty("includeVersions")]
        [JSDoc("Gets or sets a value that determines what content is selected for export based on version information. Possible Values: All, CurrentVersion, LastMajor, LastMajorAndMinor")]
        public string IncludeVersions
        {
            get
            {
                return m_exportSettings.IncludeVersions.ToString();
            }
            set
            {
                SPIncludeVersions includeVersions;
                if (value.TryParseEnum(true, out includeVersions))
                    m_exportSettings.IncludeVersions = includeVersions;
            }
        }

        [JSProperty(Name = "logExportObjectsTable")]
        [JsonProperty("logExportObjectsTable")]
        public bool LogExportObjectsTable
        {
            get
            {
                return m_exportSettings.LogExportObjectsTable;
            }
            set
            {
                m_exportSettings.LogExportObjectsTable = value;
            }
        }

        [JSProperty(Name = "logFilePath")]
        [JsonProperty("logFilePath")]
        [JSDoc("Gets or sets a value specifying the full path to the content migration log file.")]
        public string LogFilePath
        {
            get
            {
                return m_exportSettings.LogFilePath;
            }
            set
            {
                m_exportSettings.LogFilePath = value;
            }
        }

        [JSProperty(Name = "overwriteExistingDataFile")]
        [JsonProperty("overwriteExistingDataFile")]
        [JSDoc("Gets or sets a value indicating whether you can overwrite an existing content migration package file when running export.")]
        public bool OverwriteExistingDataFile
        {
            get
            {
                return m_exportSettings.OverwriteExistingDataFile;
            }
            set
            {
                m_exportSettings.OverwriteExistingDataFile = value;
            }
        }

        [JSProperty(Name = "siteUrl")]
        [JsonProperty("siteUrl")]
        [JSDoc("Gets or sets the absolute URL of a source or destination Web site. ")]
        public string SiteUrl
        {
            get
            {
                return m_exportSettings.SiteUrl;
            }
            set
            {
                m_exportSettings.SiteUrl = value;
            }
        }

        [JSProperty(Name = "testRun")]
        [JsonProperty("testRun")]
        [JSDoc("Gets or sets a value specifying whether to complete a test run.")]
        public bool TestRun
        {
            get
            {
                return m_exportSettings.TestRun;
            }
            set
            {
                m_exportSettings.TestRun = value;
            }
        }

        [JSProperty(Name = "unattachedContentDatabase")]
        [JsonIgnore]
        public SPContentDatabaseInstance UnattachedContentDatabase
        {
            get
            {
                return new SPContentDatabaseInstance(this.Engine.Object.InstancePrototype, m_exportSettings.UnattachedContentDatabase);
            }
            set
            {
                m_exportSettings.UnattachedContentDatabase = value == null
                  ? null
                  : value.SPContentDatabase;
            }
        }

        [JSFunction(Name = "validate")]
        public void Validate()
        {
            m_exportSettings.Validate();
        }
    }
}

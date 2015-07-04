﻿namespace Barista.SharePoint
{
    using System.IO;
    using Barista.Bundles;
    using Barista.Engine;
    using Barista.Library;
    using Barista.Newtonsoft.Json;
    using Barista.SharePoint.Bundles;
    using Barista.SharePoint.Library;
    using Barista.SharePoint.Search.Bundles;
    using Jurassic;
    using Jurassic.Library;
    using System;
    using System.Web;
    using Ninject;
    using Ninject.Extensions.Conventions;

    public class SPBaristaJurassicScriptEngineFactory : ScriptEngineFactory
    {
        /// <summary>
        /// Returns a new instance of a script engine object with all runtime objects available.
        /// </summary>
        /// <returns></returns>
        public override IScriptEngine GetScriptEngine(WebBundleBase webBundle, out bool isNewScriptEngineInstance, out bool errorInInitialization)
        {
            isNewScriptEngineInstance = false;
            errorInInitialization = false;

            //Based on the instancing mode, either retrieve the ScriptEngine from the desired store, or create a new ScriptEngine instance.
            var instanceSettings = SPBaristaContext.Current.Request.ParseInstanceSettings();

            ScriptEngine engine;
            switch (instanceSettings.InstanceMode)
            {
                case BaristaInstanceMode.PerCall:
                    //Always create a new instance of the script engine.
                    engine = new ScriptEngine();
                    isNewScriptEngineInstance = true;
                    break;
                case BaristaInstanceMode.Single:
                    engine = BaristaSharePointGlobal.GetOrCreateScriptEngineInstanceFromRuntimeCache(instanceSettings.InstanceName, out isNewScriptEngineInstance);
                    break;
                case BaristaInstanceMode.PerSession:
                    engine = BaristaSharePointGlobal.GetOrCreateScriptEngineInstanceFromSession(instanceSettings.InstanceName, out isNewScriptEngineInstance);
                    break;
                default:
                    throw new NotImplementedException("The instance mode of " + instanceSettings.InstanceMode + " is currently not supported.");
            }

            if (SPBaristaContext.Current.Request.ShouldForceStrict())
            {
                engine.ForceStrictMode = true;
            }

            if (isNewScriptEngineInstance)
            {
                var console = new FirebugConsole(engine)
                {
                    Output = new SPBaristaConsoleOutput(engine)
                };

                //Register Bundles.
                var instance = new BaristaSharePointGlobal(engine);

                if (webBundle != null)
                    instance.Common.RegisterBundle(webBundle);

                var binDirectory = "";
                if (HttpRuntime.AppDomainAppId != null)
                    binDirectory = HttpRuntime.BinDirectory;

                instance.Common.RegisterBundle(new StringBundle());
                instance.Common.RegisterBundle(new SugarBundle());
                instance.Common.RegisterBundle(new SucraloseBundle());
                instance.Common.RegisterBundle(new LoDashBundle());
                instance.Common.RegisterBundle(new SPWebOptimizationBundle());
                instance.Common.RegisterBundle(new MomentBundle());
                instance.Common.RegisterBundle(new MustacheBundle());
                instance.Common.RegisterBundle(new LinqBundle());
                instance.Common.RegisterBundle(new JsonDataBundle());
                instance.Common.RegisterBundle(new SharePointBundle());
                instance.Common.RegisterBundle(new SharePointSearchBundle());
                instance.Common.RegisterBundle(new SharePointPublishingBundle());
                instance.Common.RegisterBundle(new SharePointContentMigrationBundle());
                instance.Common.RegisterBundle(new SharePointTaxonomyBundle());
                instance.Common.RegisterBundle(new SharePointWorkflowBundle());
                instance.Common.RegisterBundle(new SPActiveDirectoryBundle());
                instance.Common.RegisterBundle(new SPDocumentBundle());
                instance.Common.RegisterBundle(new DiagnosticsBundle());
                instance.Common.RegisterBundle(new iCalBundle());
                instance.Common.RegisterBundle(new SmtpBundle());
                instance.Common.RegisterBundle(new K2Bundle());
                instance.Common.RegisterBundle(new UtilityBundle());
                instance.Common.RegisterBundle(new UlsLogBundle());
                instance.Common.RegisterBundle(new DocumentStoreBundle());
                instance.Common.RegisterBundle(new SimpleInheritanceBundle());
                instance.Common.RegisterBundle(new SqlDataBundle());
                instance.Common.RegisterBundle(new StateMachineBundle());
                instance.Common.RegisterBundle(new DeferredBundle());
                instance.Common.RegisterBundle(new TfsBundle());
                instance.Common.RegisterBundle(new BaristaSearchIndexBundle());
                instance.Common.RegisterBundle(new WebAdministrationBundle());
                instance.Common.RegisterBundle(new UnitTestingBundle());
                instance.Common.RegisterBundle(new WkHtmlToPdf.Library.WkHtmlToPdfBundle(binDirectory));

                //Let's do some DI
                var kernel = new StandardKernel();
                kernel.Bind(x => x
                    .FromAssembliesInPath(Path.Combine(binDirectory, "Bundles"))
                    .SelectAllClasses()
                    .InheritedFrom<IBundle>()
                    .BindAllInterfaces()
                    );

                foreach (var bundle in kernel.GetAll<IBundle>())
                {
                    instance.Common.RegisterBundle(bundle);
                }

                //Global Types
                engine.SetGlobalValue("barista", instance);

                //engine.SetGlobalValue("file", new FileSystemInstance(engine));

                engine.SetGlobalValue("Guid", new GuidConstructor(engine));
                engine.SetGlobalValue("HashTable", new HashtableConstructor(engine));
                engine.SetGlobalValue("Uri", new UriConstructor(engine));
                engine.SetGlobalValue("Encoding", new EncodingInstance(engine.Object.InstancePrototype));

                engine.SetGlobalValue("NetworkCredential", new NetworkCredentialConstructor(engine));
                engine.SetGlobalValue("Base64EncodedByteArray", new Base64EncodedByteArrayConstructor(engine));

                engine.SetGlobalValue("console", console);

                
                //If we came from the Barista event receiver, set the appropriate context.
                if (
                  SPBaristaContext.Current.Request != null &&
                  SPBaristaContext.Current.Request.ExtendedProperties != null &&
                  SPBaristaContext.Current.Request.ExtendedProperties.ContainsKey("SPItemEventProperties"))
                {
                    var properties =
                      SPBaristaContext.Current.Request.ExtendedProperties["SPItemEventProperties"];

                    var itemEventProperties = JsonConvert.DeserializeObject<BaristaItemEventProperties>(properties);
                    engine.SetGlobalValue("CurrentItemEventProperties",
                      new BaristaItemEventPropertiesInstance(engine.Object.InstancePrototype, itemEventProperties));
                }

                //Map Barista functions to global functions.
                engine.Execute(@"var help = function(obj) { return barista.help(obj); };
var require = function(name) { return barista.common.require(name); };
var listBundles = function() { return barista.common.listBundles(); };
var define = function() { return barista.common.define(arguments[0], arguments[1], arguments[2], arguments[3]); };
var include = function(scriptUrl) { return barista.include(scriptUrl); };");

                //Execute any instance initialization code.
                if (String.IsNullOrEmpty(instanceSettings.InstanceInitializationCode))
                    return engine;

                var initializationScriptSource =
                    new StringScriptSource(SPBaristaContext.Current.Request.InstanceInitializationCode, SPBaristaContext.Current.Request.InstanceInitializationCodePath);

                try
                {
                    engine.Execute(initializationScriptSource);
                }
                catch (JavaScriptException ex)
                {
                    BaristaDiagnosticsService.Local.LogException(ex, BaristaDiagnosticCategory.JavaScriptException,
                        "A JavaScript exception was thrown while evaluating script: ");
                    UpdateResponseWithJavaScriptExceptionDetails(engine, ex, SPBaristaContext.Current.Response);
                    errorInInitialization = true;

                    switch (instanceSettings.InstanceMode)
                    {
                        case BaristaInstanceMode.Single:
                            BaristaSharePointGlobal.RemoveScriptEngineInstanceFromRuntimeCache(
                                instanceSettings.InstanceName);
                            break;
                        case BaristaInstanceMode.PerSession:
                            BaristaSharePointGlobal.RemoveScriptEngineInstanceFromRuntimeCache(
                                instanceSettings.InstanceName);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    BaristaDiagnosticsService.Local.LogException(ex, BaristaDiagnosticCategory.Runtime,
                        "An internal error occured while evaluating script: ");
                    errorInInitialization = true;
                    switch (instanceSettings.InstanceMode)
                    {
                        case BaristaInstanceMode.Single:
                            BaristaSharePointGlobal.RemoveScriptEngineInstanceFromRuntimeCache(
                                instanceSettings.InstanceName);
                            break;
                        case BaristaInstanceMode.PerSession:
                            BaristaSharePointGlobal.RemoveScriptEngineInstanceFromRuntimeCache(
                                instanceSettings.InstanceName);
                            break;
                    }
                    throw;
                }
            }

            return engine;
        }
    }
}

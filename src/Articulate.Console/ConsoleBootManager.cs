using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Reflection;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.UmbracoSettings;

namespace Articulate.Console
{
    public class ConsoleBootManager : CoreBootManager
    {
        private readonly DirectoryInfo _umbracoFolder;

        public ConsoleBootManager(UmbracoApplicationBase umbracoApplication, DirectoryInfo umbracoFolder)
            : base(umbracoApplication)
        {
            _umbracoFolder = umbracoFolder;
        }

        /// <summary>
        /// Fires first in the application startup process before any customizations can occur
        /// </summary>
        /// <returns/>
        public override IBootManager Initialize()
        {
            //Go read the umbraco configuration, get the umbracoSettings and set it dynamically
            var configFile = new FileInfo(Path.Combine(_umbracoFolder.FullName, "web.config"));
            var configMap = new ExeConfigurationFileMap
            {
                ExeConfigFilename = configFile.FullName
            };
            var config = ConfigurationManager.OpenMappedExeConfiguration(configMap, ConfigurationUserLevel.None);
            
            ConfigureUmbracoSettings(config);
            ConfigureConnectionStrings(config);
            ConfigureAppSettings(config);

            return base.Initialize();
        }

        /// <summary>
        /// The main problem with booting umbraco is all of the startup handlers that will not work without a web context or in a standalone
        /// mode. So this code removes all of those handlers. We of course need some of them so this attempts to just keep the startup handlers
        /// declared inside of Umbraco.Core.
        /// </summary>
        protected override void InitializeApplicationEventsResolver()
        {
            base.InitializeApplicationEventsResolver();

            //now remove what we want to , unfortunately this needs reflection currently
            var appEventsResolverType = Type.GetType("Umbraco.Core.ObjectResolution.ApplicationEventsResolver,Umbraco.Core", true);
            var appEventsResolver = appEventsResolverType.GetStaticProperty("Current");
            //now we want to get all IApplicationStartupHandlers from the PluginManager, again, needs reflection
            var startupHandlers = (IEnumerable<Type>)PluginManager.Current.CallMethod("ResolveApplicationStartupHandlers");
            //for now we're just going to remove any type that does not exist in Umbraco.Core
            foreach (var startupHandler in startupHandlers
                .Where(x => x.Namespace != null)
                .Where(x => !x.Namespace.StartsWith("Umbraco.Core")))
            {
                //This is a special case because we have legacy handlers that are not of type IApplicationEventHandler and only 
                // of type IUmbracoStartupHandler which will throw if we try to remove them here because those are handled on
                // an internal object inside of ApplicationEventsResolver. It's our hope that none of those handlers will interfere with
                // the core processing outside of the web... but we'll have to deal with that later since I'm sure there will be problems.
                if (typeof (IApplicationEventHandler).IsAssignableFrom(startupHandler))
                {
                    appEventsResolver.CallMethod("RemoveType", infos => infos.FirstOrDefault(x => x.IsGenericMethod == false), startupHandler);    
                }
            }
        }

        private void ConfigureConnectionStrings(Configuration config)
        {
            //Important so things like SQLCE works
            AppDomain.CurrentDomain.SetData("DataDirectory", Path.Combine(_umbracoFolder.FullName, "App_Data"));

            //Hack to be able to set configuration strings at runtime, needs reflection due to how MS built it
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var readonlyField = typeof(ConfigurationElementCollection).GetField("bReadOnly", flags);
            readonlyField.SetValue(ConfigurationManager.ConnectionStrings, false);

            foreach (var connectionString in config.ConnectionStrings.ConnectionStrings.Cast<ConnectionStringSettings>())
            {
                ConfigurationManager.ConnectionStrings.Add(connectionString);
            }

            readonlyField.SetValue(ConfigurationManager.ConnectionStrings, true);
        }

        private void ConfigureAppSettings(Configuration config)
        {
            foreach (var setting in config.AppSettings.Settings.Cast<KeyValueConfigurationElement>())
            {
                ConfigurationManager.AppSettings.Set(setting.Key, setting.Value);
            }
        }

        private void ConfigureUmbracoSettings(Configuration config)
        {
            var umbSettings = (IUmbracoSettingsSection)config.GetSection("umbracoConfiguration/settings");
            //use reflection to set the settings
            UmbracoConfig.For.CallMethod("SetUmbracoSettings", umbSettings);
        }
    }
}
﻿namespace Exchange.DkimSigner
{
    using System;
    using System.Collections.Generic;
    using System.Configuration;
    using System.IO;
    using System.Reflection;
    using Microsoft.Exchange.Data.Transport;
    using Microsoft.Exchange.Data.Transport.Routing;
    using Exchange.DkimSigner.Properties;
    using System.Xml;
    using ConfigurationSettings;

    /// <summary>
    /// Creates new instances of the DkimSigningRoutingAgent.
    /// </summary>
    public sealed class DkimSigningRoutingAgentFactory : RoutingAgentFactory
    {
        /// <summary>
        /// The algorithm that should be used in signing.
        /// </summary>
        private DkimAlgorithmKind algorithm;

        /// <summary>
        /// The headers to sign in each message.
        /// </summary>
        private IEnumerable<string> headersToSign;

        /// <summary>
        /// The list of domains loaded from config file.
        /// </summary>
        private List<DomainElement> domainSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="DkimSigningRoutingAgentFactory"/> class.
        /// </summary>
        public DkimSigningRoutingAgentFactory()
        {
            this.Initialize();
        }

        /// <summary>
        /// When overridden in a derived class, the 
        /// <see cref="M:Microsoft.Exchange.Data.Transport.Routing.RoutingAgentFactory.CreateAgent(Microsoft.Exchange.Data.Transport.SmtpServer)"/> 
        /// method returns an instance of a routing agent.
        /// </summary>
        /// <param name="server">The server on which the routing agent will operate.</param>
        /// <returns>The <see cref="DkimSigningRoutingAgent"/> instance.</returns>
        public override RoutingAgent CreateAgent(SmtpServer server)
        {
            Logger.LogInformation("Creating new instance of DkimSigningRoutingAgent.");

            var dkimSigner = new DefaultDkimSigner(
                this.algorithm,
                this.headersToSign,
                domainSettings);

            return new DkimSigningRoutingAgent(dkimSigner);
        }
        
        /// <summary>
        /// Initializes various settings based on configuration.
        /// </summary>
        private void Initialize()
        {

            AppSettings = GetCustomConfig<General>("customSection/general");
            // Load the signing algorithm.
            try
            {
                this.algorithm = (DkimAlgorithmKind)Enum.Parse(typeof(DkimAlgorithmKind), AppSettings.Algorithm, true);
            }
            catch (Exception ex)
            {
                throw new ConfigurationErrorsException(Resources.DkimSigningRoutingAgentFactory_BadAlgorithmConfig, ex);
            }

            // Load the list of headers to sign in each message.
            var unparsedHeaders = AppSettings.HeadersToSign;
            if (unparsedHeaders != null)
            {
                this.headersToSign = unparsedHeaders
                    .Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            }

            DomainSection domains = GetCustomConfig<DomainSection>("domainSection");
            if (domains == null)
            {
                Logger.LogError("domainSection not found in configuration.");
                return;
            }

            domainSettings = new List<DomainElement>();
            foreach (DomainElement e in domains.Domains)
            {
                if (e.initElement(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)))
                    domainSettings.Add(e);

            }

            Logger.LogInformation("Exchange DKIM started. Algorithm: " + algorithm.ToString() + " Number of domains: " + domainSettings.Count);
        }

        private static Assembly configurationDefiningAssembly;

        public static General AppSettings;

        public static TConfig GetCustomConfig<TConfig>(string sectionName) where TConfig : ConfigurationSection
        {
            AppDomain.CurrentDomain.AssemblyResolve += new
                ResolveEventHandler(ConfigResolveEventHandler);
            configurationDefiningAssembly = Assembly.LoadFrom(Assembly.GetExecutingAssembly().Location);
            var exeFileMap = new ExeConfigurationFileMap();
            exeFileMap.ExeConfigFilename = Assembly.GetExecutingAssembly().Location + ".config";
            var customConfig = ConfigurationManager.OpenMappedExeConfiguration(exeFileMap,
                ConfigurationUserLevel.None);
            var returnConfig = customConfig.GetSection(sectionName) as TConfig;
            AppDomain.CurrentDomain.AssemblyResolve -= ConfigResolveEventHandler;
            return returnConfig;
        }

        private static Assembly ConfigResolveEventHandler(object sender, ResolveEventArgs args)
        {
            return configurationDefiningAssembly;
        }
    }
}

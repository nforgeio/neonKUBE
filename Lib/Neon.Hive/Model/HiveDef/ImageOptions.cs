//-----------------------------------------------------------------------------
// FILE:	    ImageOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// Specifies the Docker images that are to be deployed to provide various
    /// hive services.  Normally, you'll want to leave these alone because the
    /// defaults will work well for the vast majority of hives, but sometimes
    /// you may need to customize one or more of these for development, testing,
    /// or to work around problems.
    /// </summary>
    public class ImageOptions
    {
        private const string defaultRegistryCache   = HiveConst.NeonProdRegistry + "/neon-registry-cache:latest";
        private const string defaultProxy           = HiveConst.NeonProdRegistry + "/neon-proxy:latest";
        private const string defaultProxyVault      = HiveConst.NeonProdRegistry + "/neon-proxy-vault:latest";
        private const string defaultProxyManager    = HiveConst.NeonProdRegistry + "/neon-proxy-manager:latest";
        private const string defaultHiveManager     = HiveConst.NeonProdRegistry + "/neon-hive-manager:latest";
        private const string defaultDns             = HiveConst.NeonProdRegistry + "/neon-dns:latest";
        private const string defaultDnsMon          = HiveConst.NeonProdRegistry + "/neon-dns-mon:latest";
        private const string defaultProxyCache      = HiveConst.NeonProdRegistry + "/neon-proxy-cache:latest";
        private const string defaultSecretRetriever = HiveConst.NeonProdRegistry + "/neon-secret-retriever:latest";
        private const string defaultHiveMQ          = HiveConst.NeonProdRegistry + "/neon-hivemq:latest";
        private const string defaultLogHost         = HiveConst.NeonProdRegistry + "/neon-log-host:latest";
        private const string defaultLogCollector    = HiveConst.NeonProdRegistry + "/neon-log-collector:latest";
        private const string defaultElasticsearch   = HiveConst.NeonProdRegistry + "/elasticsearch:latest";
        private const string defaultKibana          = HiveConst.NeonProdRegistry + "/kibana:latest";
        private const string defaultMetricbeat      = HiveConst.NeonProdRegistry + "/metricbeat:latest";

        /// <summary>
        /// This is a regex we'll use to validate Docker image references.  I obtained
        /// this from here: https://stackoverflow.com/questions/39671641/regex-to-parse-docker-tag
        /// </summary>
        private static Regex imageRegex = new Regex(@"^(?:(?=[^:\/]{1,253})(?!-)[a-zA-Z0-9-]{1,63}(?<!-)(?:\.(?!-)[a-zA-Z0-9-]{1,63}(?<!-))*(?::[0-9]{1,5})?/)?((?![._-])(?:[a-z0-9._-]*)(?<![._-])(?:/(?![._-])[a-z0-9._-]*(?<![._-]))*)(?::(?![.-])[a-zA-Z0-9_.-]{1,128})?$");

        /// <summary>
        /// The Docker image to be used to deploy the registry cache.
        /// This defaults to <b>nhive/neon-registry-cache:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "RegistryCache", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultRegistryCache)]
        public string RegistryCache { get; set; } = defaultRegistryCache;

        /// <summary>
        /// The Docker image to be used to provision public and private proxies and proxy bridges
        /// on hive pets.  This defaults to <b>nhive/neon-proxy:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Proxy", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultProxy)]
        public string Proxy { get; set; } = defaultProxy;

        /// <summary>
        /// The Docker image to be used to provision HashiCorp Vault proxies.
        /// This defaults to <b>nhive/neon-proxy-vault:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ProxyVault", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultProxyVault)]
        public string ProxyVault { get; set; } = defaultProxyVault;

        /// <summary>
        /// The Docker image to be used to provision the <b>neon-proxy-manager</b>
        /// service.   This defaults to <b>nhive/neon-proxy-manager:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ProxyManager", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultProxyManager)]
        public string ProxyManager { get; set; } = defaultProxyManager;

        /// <summary>
        /// The Docker image to be used to provision the <b>neon-hive-manager</b>
        /// service.   This defaults to <b>nhive/neon-hive-manager:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "HiveManager", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultHiveManager)]
        public string HiveManager { get; set; } = defaultHiveManager;

        /// <summary>
        /// The Docker image to be used to provision the <b>neon-dns</b> service.
        /// This defaults to <b>nhive/neon-dns:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Dns", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultDns)]
        public string Dns { get; set; } = defaultDns;

        /// <summary>
        /// The Docker image to be used to provision the <b>neon-dns-mon</b> service.
        /// This defaults to <b>nhive/neon-dns-mon:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "DnsMon", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultDnsMon)]
        public string DnsMon { get; set; } = defaultDnsMon;

        /// <summary>
        /// The Docker image to be used to provision the <b>neon-proxy-cache</b> service.
        /// This defaults to <b>nhive/neon-proxy-cache:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "ProxyCache", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultProxyCache)]
        public string ProxyCache { get; set; } = defaultProxyCache;

        /// <summary>
        /// The Docker image to be used to retrieve Docker secrets.
        /// This defaults to <b>nhive/neon-secret-retriever:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "SecretRetriever", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultSecretRetriever)]
        public string SecretRetriever { get; set; } = defaultSecretRetriever;

        /// <summary>
        /// The Docker image to be used to provision the <b>neon-hivemq</b> service.
        /// This defaults to <b>nhive/neon-hivemq:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "HiveMQ", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(defaultHiveMQ)]
        public string HiveMQ { get; set; } = defaultHiveMQ;

        /// <summary>
        /// Identifies the <b>Elasticsearch</b> container image to be deployed on the hive to persist
        /// hive log events.  This defaults to <b>nhive/elasticsearch:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Elasticsearch", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultElasticsearch)]
        public string Elasticsearch { get; set; } = defaultElasticsearch;

        /// <summary>
        /// Identifies the <b>Kibana</b> container image to be deployed on the hive to present
        /// the hive logging user interface.  This defaults to <b>nhive/kibana:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Kibana", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultKibana)]
        public string Kibana { get; set; } = defaultKibana;

        /// <summary>
        /// Identifies the <b>Elastic Metricbeat</b> container image to be run on each node of the hive to capture
        /// Docker host node metrics.  This defaults to <b>nhive/metricbeat:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Metricbeat", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultMetricbeat)]
        public string Metricbeat { get; set; } = defaultMetricbeat;

        /// <summary>
        /// Identifies the <b>td-agent</b> service image to be run locally on every manager and worker node.  This container
        /// acts as the entrypoint to the hive's log aggregation pipeline.  This defaults to <b>nhive/neon-log-host:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "LogHost", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultLogHost)]
        public string LogHost { get; set; } = defaultLogHost;

        /// <summary>
        /// Identifies the <b>td-agent</b> container image to be run on the hive, acting as the downstream event 
        /// aggregator for all of the hive nodes.  This defaults to <b>nhive/neon-log-collector:latest</b>.
        /// </summary>
        [JsonProperty(PropertyName = "LogCollector", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultLogCollector)]
        public string LogCollector { get; set; } = defaultLogCollector;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="HiveDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(HiveDefinition hiveDefinition)
        {
            RegistryCache   = VerifyImage(nameof(RegistryCache), RegistryCache ?? defaultRegistryCache);
            Proxy           = VerifyImage(nameof(Proxy), Proxy ?? defaultProxy);
            ProxyVault      = VerifyImage(nameof(ProxyVault), ProxyVault ?? defaultProxyVault);
            ProxyManager    = VerifyImage(nameof(ProxyManager), ProxyManager ?? defaultProxyManager);
            HiveManager     = VerifyImage(nameof(HiveManager), HiveManager ?? defaultHiveManager);
            Dns             = VerifyImage(nameof(Dns), Dns ?? defaultDns);
            DnsMon          = VerifyImage(nameof(DnsMon), DnsMon ?? defaultDnsMon);
            ProxyCache         = VerifyImage(nameof(ProxyCache), ProxyCache ?? defaultProxyCache);
            SecretRetriever = VerifyImage(nameof(SecretRetriever), SecretRetriever ?? defaultSecretRetriever);
            HiveMQ          = VerifyImage(nameof(HiveMQ), HiveMQ ?? defaultHiveMQ);
            Elasticsearch   = VerifyImage(nameof(Elasticsearch), Elasticsearch ?? defaultElasticsearch);
            Kibana          = VerifyImage(nameof(Kibana), Kibana ?? defaultKibana);
            LogHost         = VerifyImage(nameof(LogHost), LogHost ?? defaultLogHost);
            LogCollector    = VerifyImage(nameof(LogCollector), LogCollector ?? defaultLogCollector);
            Metricbeat      = VerifyImage(nameof(Metricbeat), Metricbeat ?? defaultMetricbeat);
        }

        /// <summary>
        /// Verifies that a Docker image reference looks valid.
        /// </summary>
        /// <param name="propertyName">The image property name.</param>
        /// <param name="image">The image reference to be checked.</param>
        /// <returns>The image reference.</returns>
        /// <exception cref="HiveDefinitionException">Thrown if the image reference is not valid.</exception>
        private string VerifyImage(string propertyName, string image)
        {
            if (image == null || !imageRegex.IsMatch(image))
            {
                throw new HiveDefinitionException($"[{nameof(ImageOptions)}={image}] is not a valid Docker image reference.");
            }

            return image;
        }
    }
}


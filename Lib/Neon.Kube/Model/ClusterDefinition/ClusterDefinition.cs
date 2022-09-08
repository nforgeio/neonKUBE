//-----------------------------------------------------------------------------
// FILE:	    ClusterDefinition.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Describes a Kubernetes cluster.
    /// </summary>
    public class ClusterDefinition
    {
        //---------------------------------------------------------------------
        // Static members

        private const string        defaultProvisioner = "unknown";
        private readonly string[]   defaultTimeSources = new string[] { "pool.ntp.org" };

        // $todo(jefflill):
        //
        // [DnsNameRegex] and [NameRegex] need to be more restrictive than they are now by
        // ensuring that all segments start and end with letters/digits, and that dots/dash
        // rules are also applied.  We could also check for maximum lengths if we really
        // wanted to get fancy.

        /// <summary>
        /// Regex for verifying cluster names for hosts, routes, groups, etc.  This also can
        /// be used to (lightly) validate DNS host names.
        /// </summary>
        public static Regex DnsNameRegex { get; private set; } = new Regex(@"^[a-z0-9.\-_]+$", RegexOptions.IgnoreCase);

        /// <summary>
        /// Regex for verifying non-DNS like names that start and end with a letter or digit and
        /// may also include dashes.
        /// </summary>
        public static Regex NameRegex { get; private set; } = new Regex(@"^[a-z0-9\-_]+$", RegexOptions.IgnoreCase);

        /// <summary>
        /// Regex for verifying cluster prefixes.  This is the similar to <see cref="DnsNameRegex"/> but optionally
        /// allows a "(" and ")" which we use for clusterspace related deployments.
        /// </summary>
        public static Regex PrefixRegex { get; private set; } = new Regex(@"^[a-z0-9.\-_()]+$", RegexOptions.IgnoreCase);

        /// <summary>
        /// The prefix reserved for neonKUBE specific annotations and labels.
        /// </summary>
        public const string ReservedPrefix = "neonkube.io/";

        /// <summary>
        /// Parses and validates a YAML cluster definition file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="strict">Optionally require that all input properties map to <see cref="ClusterDefinition"/> properties.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        /// <exception cref="IOException">Thrown if the file could not be read.</exception>
        public static void ValidateFile(string path, bool strict = false)
        {
            FromFile(path, strict: strict);
        }

        /// <summary>
        /// Parses a cluster definition from YAML text.
        /// </summary>
        /// <param name="yaml">The JSON text.</param>
        /// <param name="strict">Optionally require that all input properties map to <see cref="ClusterDefinition"/> properties.</param>
        /// <param name="validate">
        /// <para>
        /// Optionally validate the cluster definition.
        /// </para>
        /// <note>
        /// You must have already called <b>HostingLoader.Initialize()</b> for 
        /// validation to work.
        /// </note>
        /// </param>
        /// <returns>The parsed <see cref="ClusterDefinition"/>.</returns>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        /// <remarks>
        /// <note>
        /// The source is first preprocessed using <see cref="PreprocessReader"/>
        /// and then is parsed as YAML.
        /// </note>
        /// </remarks>
        public static ClusterDefinition FromYaml(string yaml, bool strict = false, bool validate = false)
        {
            Covenant.Requires<ArgumentNullException>(yaml != null, nameof(yaml));

            using (var stringReader = new StringReader(yaml))
            {
                using (var preprocessReader = new PreprocessReader(stringReader))
                {
                    preprocessReader.SetYamlMode();

                    var clusterDefinition = NeonHelper.YamlDeserialize<ClusterDefinition>(preprocessReader.ReadToEnd(), strict: strict);

                    PopulateNodeNames(clusterDefinition);

                    if (validate)
                    {
                        clusterDefinition.Validate();
                    }

                    return clusterDefinition;
                }
            }
        }

        /// <summary>
        /// Parses a YAML cluster definition from a file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="strict">Optionally require that all input properties map to <see cref="ClusterDefinition"/> properties.</param>
        /// <returns>The parsed <see cref="ClusterDefinition"/>.</returns>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        /// <exception cref="IOException">Thrown if the file could not be read.</exception>
        /// <remarks>
        /// <note>
        /// The source is first preprocessed using <see cref="PreprocessReader"/>
        /// and then is parsed as JSON.
        /// </note>
        /// </remarks>
        public static ClusterDefinition FromFile(string path, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(path != null, nameof(path));

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (var reader = new StreamReader(stream))
                {
                    using (var preprocessReader = new PreprocessReader(reader))
                    {
                        preprocessReader.SetYamlMode();

                        preprocessReader.ProcessStatements = true;

                        var clusterDefinition = NeonHelper.YamlDeserialize<ClusterDefinition>(preprocessReader.ReadToEnd(), strict: strict);

                        if (clusterDefinition == null)
                        {
                            throw new ArgumentException($"Invalid cluster definition in [{path}].", nameof(path));
                        }

                        PopulateNodeNames(clusterDefinition);
                        clusterDefinition.Validate();

                        return clusterDefinition;
                    }
                }
            }
        }

        /// <summary>
        /// Populates the <see cref="NodeDefinition.Name"/> properties from its dictionary name.
        /// </summary>
        /// <param name="clusterDefinition"></param>
        private static void PopulateNodeNames(ClusterDefinition clusterDefinition)
        {
            foreach (var item in clusterDefinition.NodeDefinitions)
            {
                var node = item.Value;

                if (string.IsNullOrEmpty(node.Name))
                {
                    node.Name = item.Key;
                }
                else if (item.Key != node.Name)
                {
                    throw new FormatException($"The node names don't match [\"{item.Key}\" != \"{node.Name}\"].");
                }
            }
        }

        /// <summary>
        /// Verifies that a string is a valid cluster name.
        /// </summary>
        /// <param name="name">The name being tested.</param>
        /// <returns><c>true</c> if the name is valid.</returns>
        public static bool IsValidName(string name)
        {
            return name != null && DnsNameRegex.IsMatch(name);
        }

        /// <summary>
        /// Ensures that a VM memory or disk size specification is valid and also
        /// converts the value to the corresponding long count.
        /// </summary>
        /// <param name="sizeValue">The size value string.</param>
        /// <param name="optionsType">Type of the property holding the size property (used for error reporting).</param>
        /// <param name="propertyName">The size property name (used for error reporting).</param>
        /// <param name="minimum">Optionally specifies the m inimum size as <see cref="ByteUnits"/>.</param>
        /// <returns>The size converted into a <c>long</c>.</returns>
        /// <exception cref="ClusterDefinitionException">Thrown if the size is not valid.</exception>
        public static long ValidateSize(string sizeValue, Type optionsType, string propertyName, string minimum = null)
        {
            if (string.IsNullOrEmpty(sizeValue))
            {
                throw new ClusterDefinitionException($"[{optionsType.Name}.{propertyName}] cannot be NULL or empty.");
            }

            if (!ByteUnits.TryParse(sizeValue, out var size))
            {
                throw new ClusterDefinitionException($"[{optionsType.Name}.{propertyName}={sizeValue}] cannot be parsed.");
            }

            if (!string.IsNullOrEmpty(minimum) && size < ByteUnits.Parse(minimum))
            {
                throw new ClusterDefinitionException($"[{optionsType.Name}.{propertyName}={sizeValue}] cannot be less than [{minimum}].");
            }

            return (long)size;
        }

        /// <summary>
        /// Normalizes a cluster definition for <see cref="AreSimilar(ClusterDefinition, ClusterDefinition)"/>.
        /// </summary>
        /// <param name="definition">The cluster definition.</param>
        private static void Normalize(ClusterDefinition definition)
        {
            Covenant.Requires<ArgumentNullException>(definition != null, nameof(definition));

            // The domain amd public addresses aren't important to [ClusterFixture].

            definition.Domain          = null;
            definition.PublicAddresses = null;

            // Ensure that computed peroperties are set.

            definition.Validate();

            // $todo(jefflill):
            //
            // We're going to clear a bunch of the node properties that may be
            // customized during cluster setup.  This means that changes to these
            // properties will not impact [ClusterFixture]'s decision about redeploying
            // the cluster or not.
            //
            //      https://github.com/nforgeio/neonKUBE/issues/1505

            foreach (var node in definition.Nodes)
            {
                node.Ingress                        = true;
                node.Labels.StorageSize             = null;
                node.Labels.ComputeCores            = 0;
                node.Labels.ComputeRam              = 0;
                node.Labels.PhysicalLocation        = null;
                node.Labels.PhysicalMachine         = null;
                node.Labels.PhysicalAvailabilitySet = null;
                node.Labels.PhysicalPower           = null;
                node.Labels.Istio                   = true;
                node.Labels.OpenEbs                 = false;
                node.Labels.NeonSystem              = true;
                node.Labels.NeonSystemDb            = true;
                node.Labels.NeonSystemRegistry      = true;
                node.Labels.Minio                   = true;
                node.Labels.Metrics                 = true;
                node.Labels.MetricsInternal         = true;
                node.Labels.Logs                    = true;
                node.Labels.LogsInternal            = true;
                node.Labels.Traces                  = true;
                node.Labels.TracesInternal          = true;
            }
        }

        /// <summary>
        /// <para>
        /// <b>INTERNAL USE ONLY:</b> Compares two <see cref="ClusterDefinition"/> instances to 
        /// determine whether they can be considered the same by <c>ClusterFixture</c> when it's 
        /// deciding whether to reuse an existing cluster or deploy a new one.
        /// </para>
        /// <note>
        /// This method works by comparing the definitions serialized to JSON after removing a handful
        /// of unimportant properties that may conflict.
        /// </note>
        /// </summary>
        /// <param name="definition1">The first cluster definition.</param>
        /// <param name="definition2">The second cluster definition.</param>
        /// <returns><c>true</c> when the definitions are close enough for <c>ClusterFixture</c>.</returns>
        public static bool AreSimilar(ClusterDefinition definition1, ClusterDefinition definition2)
        {
            Covenant.Requires<ArgumentNullException>(definition1 != null, nameof(definition1));
            Covenant.Requires<ArgumentNullException>(definition2 != null, nameof(definition2));

            definition1 = NeonHelper.JsonClone(definition1);
            definition2 = NeonHelper.JsonClone(definition2);

            // Clear properties [ClusterFixture] doesn't care about.

            Normalize(definition1);
            Normalize(definition2);

            return NeonHelper.JsonEquals(definition1, definition2);
        }

        //---------------------------------------------------------------------
        // Instance members

        private object syncLock = new object();

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterDefinition()
        {
        }

        /// <summary>
        /// Returns <c>true</c> for cluster definitions that describe a special neonKUBE/CLOUD
        /// cluster like the the neonCLOUD built-in cluster.  This is used to relax constraints
        /// on user cluster definitions like cluster node names not being able to use the "neon-"
        /// prefix.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        internal bool IsSpecialNeonCluster
        {
            get
            {
                switch (Hosting.Environment)
                {
                    case HostingEnvironment.HyperV:

                        return Hosting.HyperV != null && Hosting.HyperV.NeonDesktopBuiltIn;

                    default:

                        return false;
                }
            }
        }

        /// <summary>
        /// Indicates whether the definition describes a neonDESKTOP built-in clusters.
        /// </summary>
        [JsonProperty(PropertyName = "IsDesktopBuiltIn", Required = Required.Always)]
        [YamlMember(Alias = "isDesktopBuiltIn", ApplyNamingConventions = false)]
        public bool IsDesktopBuiltIn { get; set; }

        /// <summary>
        /// Indicates whether the cluster should be locked after being deployed successfully.
        /// <b>neon-desktop</b>, <b>neon-cli</b>, and <b>ClusterFixture</b> will block distructive
        /// operations such as cluster <b>pause</b>, <b>reset</b>, <b>remove</b>, and <b>stop</b>
        /// on locked clusters as to help avoid impacting production clusters by accident.
        /// </summary>
        [JsonProperty(PropertyName = "IsLocked", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "isLocked", ApplyNamingConventions = false)]
        [DefaultValue(true)]
        public bool IsLocked { get; set; } = true;

        /// <summary>
        /// <para>
        /// The cluster name.
        /// </para>
        /// <note>
        /// The name may include only letters, numbers, periods, dashes, and underscores and
        /// may be up to 32 characters long.  Some hosting environments enforce length limits
        /// on resource names that we derive from the cluster name, so please limit your
        /// cluster name to 32 characters.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        public string Name { get; set; }

        /// <summary>
        /// The unique cluster ID.  This is generated during cluster setup and must not be specified by the user.
        /// </summary>
        [JsonProperty(PropertyName = "Id", Required = Required.Always)]
        [YamlMember(Alias = "id", ApplyNamingConventions = false)]
        public string Id { get; set; }

        /// <summary>
        /// Optionally describes the cluster for humans.  This may be a string up to 256 characters long.
        /// </summary>
        [JsonProperty(PropertyName = "Description", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "description", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Description { get; set; } = null;

        /// <summary>
        /// <para>
        /// The cluster DNS domain.  neonKUBE generates a domain like <b>GUID.neoncluster.io</b>
        /// for your cluster by default when this is not set.
        /// </para>
        /// <note>
        /// Setting this to a specific domain that you've already registered is not supported at
        /// this time and will be ignored.
        /// </note>
        /// </summary>
        /// <remarks>
        /// <para>
        /// The idea here is that neonKUBE will be use the generated domain to deploy a fully
        /// functional cluster out-of-the-box, with real DNS records and a SSL certificate.
        /// This works even for clusters deployed behind a firewall or neonDESKTOP built-in
        /// clusters running on a workstation or laptop.
        /// </para>
        /// <para>
        /// In the future, we plan to support custom DNS domains where these are pre-registered
        /// by the customer or we manage the DNS hosts on behalf of the customer via a domain
        /// registar API.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "Domain", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "domain", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Domain { get; set; }

        /// <summary>
        /// Returns the <b>neoncluster.io</b> domain for this cluster.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public string NeonClusterDomain => $"{Id}.neoncluster.io";

        /// <summary>
        /// <para>
        /// Optionally specifies the public IP addresses for the cluster.
        /// </para>
        /// <note>
        /// <para>
        /// For cloud clusters, this will default to the public IP address assigned to the cluster
        /// load balancer and for on-premise clusters, this defaults to the IP addresses assigned
        /// to the control-plane nodes.
        /// </para>
        /// <para>
        /// This can also be specified explicitly here in the cluster definition.  This is useful
        /// for things like documenting the public IP address for a router that directs traffic
        /// into the cluster.
        /// </para>
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "PublicAddresses", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "publicAddresses", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> PublicAddresses { get; set; } = null;

        /// <summary>
        /// Optionally specifies the semantic version of the neonKUBE cluster being created.
        /// This defaults to <c>null</c> which indicates that the latest supported cluster
        /// version will be created.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterVersion", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ClusterVersion { get; set; } = null;

        /// <summary>
        /// <para>
        /// Optionally specifies custom tags that will be attached to cluster resources in cloud
        /// hosting environments.  These tags are intended to help you manage your cloud resources
        /// as well as help organize your cost reporting.
        /// </para>
        /// <note>
        /// Currently, this is only supported for clusters deployed to AWS, Azure or Google Cloud.
        /// </note>
        /// </summary>
        public List<ResourceTag> ResourceTags { get; set; } = null;

        /// <summary>
        /// Enables or disables specific Kubernetes features.  This can be used to enable
        /// alpha quality or other features that are disabled by default for the Kubernetes
        /// version being deployed or to disable features.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is a dictionary that maps feature names a boolean where <c>true</c>
        /// enables the feature and <c>false</c> disables it.  You can find a description
        /// of the available Kubernetes feature gates here:
        /// </para>
        /// <para>
        /// https://kubernetes.io/docs/reference/command-line-tools-reference/feature-gates/#feature-gates
        /// </para>
        /// <note>
        /// Your neonKUBE cluster may be somewhat older than the current Kubernetes version,
        /// so some of the features listed may not apply to your cluster.
        /// </note>
        /// <para>
        /// neonKUBE clusters enables specific features by default when you you haven't
        /// explicitly disabled them via this property.  Note that some features are 
        /// required and cannot be disabled.
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>EphemeralContainers</b></term>
        ///     <description>
        ///     <para>
        ///     Enables the ability to add ephemeral containers to running pods.
        ///     </para>
        ///     <para>
        ///     This is very handy for debugging pods.
        ///     </para>
        ///     </description>
        /// </item>
        /// </list>
        /// </remarks>
        [JsonProperty(PropertyName = "FeatureGates", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "featureGates", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Dictionary<string, bool> FeatureGates = new Dictionary<string, bool>();

        /// <summary>
        /// Optionally specifies options used by <b>ClusterFixture</b> and possibly
        /// custom tools for customizing cluster and node names to avoid conflicts.
        /// </summary>
        [JsonProperty(PropertyName = "Deployment", Required = Required.Always)]
        [YamlMember(Alias = "deployment", ApplyNamingConventions = false)]
        public DeploymentOptions Deployment { get; set; } = new DeploymentOptions();

        /// <summary>
        /// Specifies the cluster storage related options.
        /// </summary>
        [JsonProperty(PropertyName = "Storage", Required = Required.Always)]
        [YamlMember(Alias = "storage", ApplyNamingConventions = false)]
        public StorageOptions Storage { get; set; } = new StorageOptions();

        /// <summary>
        /// Specifies cluster security options.
        /// </summary>
        [JsonProperty(PropertyName = "Security", Required = Required.Always)]
        [YamlMember(Alias = "security", ApplyNamingConventions = false)]
        public SecurityOptions Security { get; set; } = new SecurityOptions();

        /// <summary>
        /// Returns the Kubernetes cluster options.,
        /// </summary>
        [JsonProperty(PropertyName = "Kubernetes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "kubernetes", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public KubernetesOptions Kubernetes { get; set; } = new KubernetesOptions();

        /// <summary>
        /// Returns the options to be used for configuring the cluster integrated
        /// Elasticsearch/Fluentd/Kibana (Mon) logging stack.
        /// </summary>
        [JsonProperty(PropertyName = "Monitor", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "monitor", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public MonitorOptions Monitor { get; set; } = new MonitorOptions();

        /// <summary>
        /// Specifies hosting related settings (e.g. the cloud provider).  This defaults to
        /// <c>null</c> which indicates that the cluster will be hosted on private servers.
        /// </summary>
        [JsonProperty(PropertyName = "Hosting", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "hosting", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public HostingOptions Hosting { get; set; } = null;

        /// <summary>
        /// Specifies optional features to be installed in the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "Features", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "features", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public FeatureOptions Features { get; set; } = new FeatureOptions();

        /// <summary>
        /// Identifies the datacenter.  This defaults to empty string for on-premise clusters
        /// or the region for clusters deployed to the cloud.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The name may include only letters, numbers, periods, dashes, and underscores.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "Datacenter", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "datacenter", ApplyNamingConventions = false)]
        [DefaultValue("")]
        public string Datacenter { get; set; } = String.Empty;

        /// <summary>
        /// <para>
        /// Optionally specifies the latitude of the cluster location.  This is a value
        /// between -90 and +90 degrees.
        /// </para>
        /// <note>
        /// <see cref="Latitude"/> and <see cref="Longitude"/> must both be specified together or
        /// not at all.
        /// </note>
        /// </summary>
        public double? Latitude { get; set; } = null;

        /// <summary>
        /// <para>
        /// Optionally specifies the longitude of the cluster location.  This is a value
        /// between -180 and +180 degrees.
        /// </para>
        /// <note>
        /// <see cref="Latitude"/> and <see cref="Longitude"/> must both be specified together or
        /// not at all.
        /// </note>
        /// </summary>
        public double? Longitude { get; set; } = null;

        /// <summary>
        /// Indicates how the cluster is being used.
        /// </summary>
        [JsonProperty(PropertyName = "Purpose", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "purpose", ApplyNamingConventions = false)]
        [JsonConverter(typeof(StringEnumConverter))]
        [DefaultValue(ClusterPurpose.Unspecified)]
        public ClusterPurpose Purpose { get; set; } = ClusterPurpose.Unspecified;

        /// <summary>
        /// Specifies the NTP time sources to be configured for the cluster.  These are the
        /// FQDNs or IP addresses of the sources.  This defaults to <b>pool.ntp.org</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The cluster control-plane nodes will be configured to synchronize their time with these
        /// time sources and the worker nodes will be configured to synchronize their time
        /// with the control-plane nodes.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "TimeSources", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "timeSources", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string[] TimeSources { get; set; } = null;

        /// <summary>
        /// Optionally specifies one or more APT proxy/cache servers the cluster will use to install
        /// and update Linux packages.  These are endpoints like <b>HOSTNAME:PORT</b> or <b>ADDRESS.PORT</b>
        /// of a <b>apt-cacher-ng</b> or other package proxy server.  The port is generall set to <b>3142</b>
        /// Multiple proxies may be specified by separating them with spaces.  This defaults to
        /// referencing the <b>apt-cacher-ng</b> instances running on the control-plane nodes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// A package cache will greatly reduce the Internet network traffic required to deploy a
        /// cluster, especially for large clusters.
        /// </para>
        /// <note>
        /// The cluster nodes are configured to failover to different proxies or to hit the 
        /// default Linux distribution package mirror directly if any or all of the caches
        /// specified are unavailable.
        /// </note>
        /// <note>
        /// The package caches will be tried in the order they are listed.  This essentially
        /// makes the first cache primary, with the others as backups.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "PackageProxy", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "packageProxy", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string PackageProxy { get; set; } = null;

        /// <summary>
        /// Describes the cluster's network configuration.
        /// </summary>
        [JsonProperty(PropertyName = "Network", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "network", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public NetworkOptions Network { get; set; } = new NetworkOptions();

        /// <summary>
        /// Customizes the cluster's container configuration.
        /// </summary>
        [JsonProperty(PropertyName = "Container", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "container", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ContainerOptions Container { get; set; } = null;

        /// <summary>
        /// Specifies host node options.
        /// </summary>
        [JsonProperty(PropertyName = "NodeOptions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "nodeOptions", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public NodeOptions NodeOptions { get; set; } = new NodeOptions();

        /// <summary>
        /// Describes the cluster nodes.
        /// </summary>
        [JsonProperty(PropertyName = "Nodes", Required = Required.Always)]
        [YamlMember(Alias = "nodes", ApplyNamingConventions = false)]
        public Dictionary<string, NodeDefinition> NodeDefinitions { get; set; } = new Dictionary<string, NodeDefinition>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// <para>
        /// Holds temporary state required by various components during cluster setup.  This is a case-insensitive
        /// string dictionary that will be maintained during cluster setup and is persisted to disk to support 
        /// restarting and continuing cluster setup when necessary.  This property will be set to <c>null</c>
        /// after cluster setup is complete, so this is a suitable place for storing generated secure credentials.
        /// </para>
        /// <para>
        /// As a convention, dictionary keys should use a dot notation like <b>neon-cluster-operator.connstring</b>
        /// to avoid naming conflicts and to make it clear what's what during debugging.
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> Do not reference this dictionary directly.  Use <see cref="SetSetupState(string, string)"/>,
        /// <see cref="GetSetupState(string)"/>, and <see cref="RemoveSetupState(string)"/>.  This will protect the
        /// dictionary when multiple threads try to access it which is entirely possible since <see cref="SetupController{NodeMetadata}"/>
        /// implicitly performs operations using multiple threads.
        /// </note>
        /// <note>
        /// <b>IMPORTANT:</b> Any changes to this state <b>persisted automatically</b>.  You'll need to call
        /// <see cref="ClusterLogin.Save()"/> on the cluster login holding the cluster definition.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "SetupState", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "setupState", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Dictionary<string, string> SetupState { get; set; } = null;

        /// <summary>
        /// Optionally specifies the cluster root single sign-on (SSO) password.  A random password
        /// with of <see cref="SecurityOptions.PasswordLength"/> will be created by default when no
        /// password is specified here.
        /// </summary>
        [JsonProperty(PropertyName = "RootPassword", Required = Required.Default)]
        [YamlMember(Alias = "rootPassword", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string RootPassword { get; set; } = null;

        /// <summary>
        /// Adds or updates an item in <see cref="SetupState"/>.  Use this instead of accessing the dictionary
        /// directly for thread safety.
        /// </summary>
        /// <param name="key">The item key.</param>
        /// <param name="value">The item value.</param>
        public void SetSetupState(string key, string value)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(key), nameof(key));
            Covenant.Requires<ArgumentNullException>(value != null, nameof(value));

            lock (syncLock)
            {
                if (SetupState == null)
                {
                    SetupState = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
                }

                SetupState[key] = value;
            }
        }

        /// <summary>
        /// Retrieves the value of an item from <see cref="SetupState"/> when it exists, or 
        /// <c>null</c> when it does not exist.  Use this instead of accessing the dictionary
        /// directly for thread safety.
        /// </summary>
        /// <param name="key">The item key.</param>
        /// <returns>The item value or <c>null</c> if the item doesn't exist.</returns>
        public string GetSetupState(string key)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(key), nameof(key));

            lock (syncLock)
            {
                if (SetupState == null)
                {
                    SetupState = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
                }

                if (SetupState.TryGetValue(key, out var value))
                {
                    return value;
                }
                else
                {
                    return null;
                }
            }
        }

        /// <summary>
        /// Removes an item from <see cref="SetupState"/> if it exists and does notthing
        /// when the item doesn't exist.  Use this instead of accessing the dictionary
        /// directly for thread safety.
        /// </summary>
        /// <param name="key">The item key.</param>
        public void RemoveSetupState(string key)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(key), nameof(key));

            lock (syncLock)
            {
                if (SetupState == null)
                {
                    SetupState = new Dictionary<string, string>(StringComparer.CurrentCultureIgnoreCase);
                }

                if (SetupState.ContainsKey(key))
                {
                    SetupState.Remove(key);
                }
            }
        }

        /// <summary>
        /// Removes any temporary setup related state including <see cref="SetupState"/>, hosting
        /// related secrets, as well as temporary state used by the hosting managers.
        /// </summary>
        public void ClearSetupState()
        {
            lock (syncLock)
            {
                SetupState = null;

                // $todo(jefflill):
                //
                // I'm temporarily commenting this out because neon-desktop needs
                // the hosting related secrets in some circumstances.  We need to
                // think about a comprehensive solution:
                //
                //      https://github.com/nforgeio/neonKUBE/issues/1482

                // Hosting?.ClearSecrets(this);
            }
        }

        /// <summary>
        /// Enumerates all cluster node definitions.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> Nodes => NodeDefinitions.Values;

        /// <summary>
        /// Enumerates all cluster node definitions sorted in ascending order by name.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> SortedNodes => Nodes.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Enumerates the cluster control-plane node definitions.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> ControlNodes => Nodes.Where(n => n.IsControlPane);

        /// <summary>
        /// Enumerates the cluster control-plane node definitions sorted in ascending order by name.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> SortedControlNodes => ControlNodes.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Enumerates the cluster worker node definitions.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> Workers => Nodes.Where(n => n.IsWorker);

        /// <summary>
        /// Enumerates the cluster worker node definitions sorted in ascending order by name.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> SortedWorkerNodes => Workers.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Enumerates the cluster control-plane nodes sorted by name followed by the worker nodes,
        /// also sorted by name.  This is convienent for situations like assigning IP addresses
        /// or ports such that the control-plane nodes are grouped together first.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> SortedControlThenWorkerNodes => SortedControlNodes.Union(SortedWorkerNodes);

        /// <summary>
        /// Holds the subnet where nodes will be provisioned along with the name of the options 
        /// class and property where the subnet was specified for the current hosting environment.
        /// </summary>
        internal class NodeSubnetInfo
        {
            /// <summary>
            /// Constructor.
            /// </summary>
            /// <param name="subnet">The subnet.</param>
            /// <param name="property">The source property information.</param>
            /// <param name="startReservedAddresses">The number of reserved addresses at the start of the subnet.</param>
            /// <param name="endReservedAddresses">The number of reserved addresses at the end of the subnet.</param>
            public NodeSubnetInfo(string subnet, string property, int startReservedAddresses, int endReservedAddresses)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(subnet), nameof(subnet));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(property), nameof(property));

                this.Subnet                 = subnet;
                this.Property               = property;
                this.StartReservedAddresses = startReservedAddresses;
                this.EndReservedAddresses   = endReservedAddresses;
            }

            /// <summary>
            /// Returns the subnet.
            /// </summary>
            public string Subnet { get; private set; }

            /// <summary>
            /// Describes the options class and property where the subnet value was specified.
            /// </summary>
            public string Property { get; private set; }

            /// <summary>
            /// Returns the number of addresses reserved at the start of the subnet.
            /// </summary>
            public int StartReservedAddresses { get; private set; }

            /// <summary>
            /// Returns the number oaddresses reserved at the and of the subnet.
            /// </summary>
            public int EndReservedAddresses { get; private set; }

            /// <summary>
            /// Returns the total number of reserved subnet addresses.
            /// </summary>
            public int ReservedAddresses => StartReservedAddresses + EndReservedAddresses;
        }

        /// <summary>
        /// Returns the subnet where the cluster nodes will reside.  This is determined
        /// for each hosting environment.  For on-premise environments, this will be
        /// <see cref="NetworkOptions.PremiseSubnet"/>, for cloud environments, this will
        /// come from the cloud specific hosting options.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        internal NodeSubnetInfo NodeSubnet
        {
            get
            {
                // $todo(jefflill):
                //
                // This amy need to return an array of possible subnets for more advanced cloud
                // deployment scenarios (e.g. multiple availability zones or cross region
                // deployments).

                if (KubeHelper.IsOnPremiseEnvironment(Hosting.Environment))
                {
                    return new NodeSubnetInfo(Network.PremiseSubnet, $"{nameof(ClusterDefinition)}.{nameof(ClusterDefinition.Network)}.{nameof(NetworkOptions.PremiseSubnet)}", 0, 0);
                }

                switch (Hosting.Environment)
                {
                    case HostingEnvironment.Aws:

                        return new NodeSubnetInfo(
                            Hosting.Aws.Network.NodeSubnet,
                            $"{nameof(ClusterDefinition)}.{nameof(HostingOptions)}.{nameof(HostingOptions.Aws)}.{nameof(AwsHostingOptions.Network.NodeSubnet)}", 
                            KubeConst.CloudSubnetStartReservedIPs, 
                            KubeConst.CloudSubnetEndReservedIPs);

                    case HostingEnvironment.Azure:

                        return new NodeSubnetInfo(
                            Hosting.Azure.Network.NodeSubnet, 
                            $"{nameof(ClusterDefinition)}.{nameof(HostingOptions)}.{nameof(HostingOptions.Azure)}.{nameof(AzureHostingOptions.Network.NodeSubnet)}",
                            KubeConst.CloudSubnetStartReservedIPs,
                            KubeConst.CloudSubnetEndReservedIPs);

                    case HostingEnvironment.Google:

                        return new NodeSubnetInfo(
                            Hosting.Google.NodeSubnet,
                            $"{nameof(ClusterDefinition)}.{nameof(HostingOptions)}.{nameof(HostingOptions.Google)}.{nameof(GoogleHostingOptions.NodeSubnet)}",
                            KubeConst.CloudSubnetStartReservedIPs,
                            KubeConst.CloudSubnetEndReservedIPs);

                    default:
                    case HostingEnvironment.Unknown:

                        throw new NotImplementedException($"Unexpected hosting environment [{Hosting.Environment}].");
                }
            }
        }

        /// <summary>
        /// Validates that node private IP addresses are set, are within the nodes subnet, and
        /// are unique.  This method is intended to be called from hosting options classes
        /// like <see cref="MachineHostingOptions"/> which require specified node IP addresses.
        /// </summary>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void ValidatePrivateNodeAddresses()
        {
            var ipAddressToNode = new Dictionary<IPAddress, NodeDefinition>();

            foreach (var node in SortedNodes.OrderBy(n => n.Name))
            {
                if (string.IsNullOrEmpty(node.Address))
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] has not been assigned a private IP address.");
                }

                if (!NetHelper.TryParseIPv4Address(node.Address, out var address))
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] has invalid private IP address [{node.Address}].");
                }

                if (address == IPAddress.Any)
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] cannot be assigned the [0.0.0.0] IP address.");
                }

                if (ipAddressToNode.TryGetValue(address, out var conflictingNode))
                {
                    throw new ClusterDefinitionException($"Nodes [{conflictingNode.Name}] and [{node.Name}] have the same IP address [{address}].");
                }

                ipAddressToNode.Add(address, node);
            }
        }

        /// <summary>
        /// Adds a node to the cluster.
        /// </summary>
        /// <param name="node">The new node.</param>
        public void AddNode(NodeDefinition node)
        {
            Covenant.Requires<ArgumentNullException>(node != null, nameof(node));
            Covenant.Requires<ArgumentException>(NeonHelper.DoesNotThrow(() => node.Validate(this)), nameof(node));

            NodeDefinitions.Add(node.Name, node);
        }

        /// <summary>
        /// Validates the cluster definition and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate()
        {
            // Wire up the node label parents.

            foreach (var node in NodeDefinitions.Values)
            {
                if (node.Labels != null)
                {
                    node.Labels.Node = node;
                }
            }

            // Validate the properties.

            FeatureGates = FeatureGates ?? new Dictionary<string, bool>();
            Deployment   = Deployment ?? new DeploymentOptions();
            Storage      = Storage ?? new StorageOptions();
            Security     = Security ?? new SecurityOptions();
            Kubernetes   = Kubernetes ?? new KubernetesOptions();
            Monitor      = Monitor ?? new MonitorOptions();
            Hosting      = Hosting ?? new HostingOptions();
            Hosting.Vm   = Hosting.Vm ?? new VmHostingOptions();
            NodeOptions  = NodeOptions ?? new NodeOptions();
            Network      = Network ?? new NetworkOptions();
            Container    = Container ?? new ContainerOptions();
            Features     = Features ?? new FeatureOptions();

            if (IsDesktopBuiltIn && Nodes.Count() > 1)
            {
                new ClusterDefinitionException($"[{nameof(IsDesktopBuiltIn)}=true] is allowed only for single node clusters.");
            }

            if (IsDesktopBuiltIn && !IsSpecialNeonCluster)
            {
                new ClusterDefinitionException($"[{nameof(IsDesktopBuiltIn)}=true] is allowed only when [{nameof(IsSpecialNeonCluster)}=true].");
            }

            foreach (var node in Nodes)
            {
                node.Validate(this);
            }

            Deployment.Validate(this);
            Storage.Validate(this);
            Security.Validate(this);
            Kubernetes.Validate(this);
            Monitor.Validate(this);
            Network.Validate(this);
            Hosting.Validate(this);
            NodeOptions.Validate(this);
            Network.Validate(this);
            Container.Validate(this);
            Features.Validate(this);

            // Have the hosting manager perform its own validation.

            new HostingManagerFactory().Validate(this);

            // Add default neonKUBE feature gates when the user has not already configured them.

            if (!FeatureGates.ContainsKey("EphemeralContainers"))
            {
                FeatureGates["EphemeralContainers"] = true;
            }

            // Validate/initialize the public addresses.

            PublicAddresses ??= new List<string>();

            PublicAddresses = PublicAddresses
                .Where(address => !string.IsNullOrEmpty(address))
                .ToList();

            foreach (var address in PublicAddresses)
            {
                if (!IPAddress.TryParse(address, out var ip))
                {
                    throw new ClusterDefinitionException($"[{nameof(PublicAddresses)}={address}] is not a valid IPv4 address.");
                }
            }

            if (PublicAddresses.Count == 0 && !KubeHelper.IsCloudEnvironment(Hosting.Environment))
            {
                // Default to the control-plane node addresses for non-cloud environments.

                foreach (var node in SortedControlNodes)
                {
                    PublicAddresses.Add(node.Address);
                }
            }

            // Validate the NTP time sources.

            if (TimeSources == null || TimeSources.Length == 0 || TimeSources.Count(ts => string.IsNullOrWhiteSpace(ts)) > 0)
            {
                TimeSources = new string[] { "pool.ntp.org" };
            }

            // Validate the node definitions.

            if (NodeDefinitions == null || NodeDefinitions.Count == 0)
            {
                throw new ClusterDefinitionException("At least one cluster node must be defined.");
            }

            foreach (var node in NodeDefinitions.Values)
            {
                node.Validate(this);
            }

            ClusterVersion = KubeVersions.NeonKube;

            if (Name == null)
            {
                throw new ClusterDefinitionException($"The [{nameof(Name)}] property is required.");
            }

            if (!IsValidName(Name))
            {
                throw new ClusterDefinitionException($"The [{nameof(Name)}={Name}] property is not valid.  Only letters, numbers, periods, dashes, and underscores are allowed.");
            }

            if (Name.Length > 32)
            {
                throw new ClusterDefinitionException($"The [{nameof(Name)}={Name}] property has more than 32 characters.  Some hosting environments enforce name length limits so please trim your cluster name.");
            }

            if (Description != null && Description.Length > 256)
            {
                throw new ClusterDefinitionException($"The [{nameof(Description)}] property has more than 256 characters.");
            }

            if (!string.IsNullOrEmpty(Datacenter) && !IsValidName(Datacenter))
            {
                throw new ClusterDefinitionException($"The [{nameof(Datacenter)}={Datacenter}] property is not valid.  Only letters, numbers, periods, dashes, and underscores are allowed.");
            }

            if (Latitude.HasValue != Longitude.HasValue)
            {
                throw new ClusterDefinitionException($"The [{nameof(Latitude)}] and [{nameof(Longitude)}] properties must be set together or not set at all.");
            }

            if (Latitude.HasValue && (Latitude.Value < -90 || 90 < Latitude.Value))
            {
                throw new ClusterDefinitionException($"The [{nameof(Latitude)}={Latitude}] must be within: -90...+90");
            }

            if (Longitude.HasValue && (Longitude.Value < -180 || 180 < Longitude.Value))
            {
                throw new ClusterDefinitionException($"The [{nameof(Latitude)}={Latitude}] must be within: -180...+180");
            }

            var controlNodeCount = ControlNodes.Count();

            if (controlNodeCount == 0)
            {
                throw new ClusterDefinitionException("Clusters must have at least one control-plane node.");
            }
            else if (controlNodeCount > KubeConst.MaxControlNodes)
            {
                throw new ClusterDefinitionException($"Clusters may not have more than [{KubeConst.MaxControlNodes}] control-plane nodes.");
            }
            else if (!NeonHelper.IsOdd(controlNodeCount))
            {
                throw new ClusterDefinitionException($"[{controlNodeCount}] control-plane nodes is not allowed.  Only an odd number of control-plane nodes is allowed: [1, 3, 5,...]");
            }

            if (!string.IsNullOrEmpty(PackageProxy))
            {
                // Ensure that this is set to zero or more network endpoints
                // formatted like:
                //
                //      HOSTNAME:PORT
                //      ADDRESS:PORT

                foreach (var endpoint in PackageProxy.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    var fields = endpoint.Split(':');

                    if (!NetHelper.TryParseIPv4Address(fields[0], out var address) && !NetHelper.IsValidHost(fields[0]))
                    {
                        throw new ClusterDefinitionException($"Invalid IP address or HOSTNAME [{fields[0]}] in [{nameof(PackageProxy)}={PackageProxy}].");
                    }

                    if (!int.TryParse(fields[1], out var port) || !NetHelper.IsValidPort(port))
                    {
                        throw new ClusterDefinitionException($"Invalid port [{fields[1]}] in [{nameof(PackageProxy)}={PackageProxy}].");
                    }
                }
            }

            // Ensure that every node is assigned to an availability set, assigning control-plane
            // nodes to the [control-plane] set by default and worker nodes to the [worker] set.

            foreach (var node in Nodes)
            {
                if (!string.IsNullOrEmpty(node.Labels.PhysicalAvailabilitySet))
                {
                    continue;
                }

                node.Labels.PhysicalAvailabilitySet = node.IsControlPane ? "control-plane" : "worker";
            }
        }
    }
}

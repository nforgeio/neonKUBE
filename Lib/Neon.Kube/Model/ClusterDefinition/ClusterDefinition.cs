//-----------------------------------------------------------------------------
// FILE:	    ClusterDefinition.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

        private const string        defaultDatacenter  = "DATACENTER";
        private const string        defaultProvisioner = "unknown";
        private readonly string[]   defaultTimeSources = new string[] { "pool.ntp.org" };

        /// <summary>
        /// Regex for verifying cluster names for hosts, routes, groups, etc.  This also can
        /// be used to (lightly) validate DNS host names.
        /// </summary>
        public static Regex NameRegex { get; private set; } = new Regex(@"^[a-z0-9.\-_]+$", RegexOptions.IgnoreCase);

        /// <summary>
        /// Regex for verifying cluster prefixes.  This is the similar to <see cref="NameRegex"/> but optionally
        /// allows a "(" and ")" which we use for automation related deployments.
        /// </summary>
        public static Regex PrefixRegex { get; private set; } = new Regex(@"^[a-z0-9.\-_()]+$", RegexOptions.IgnoreCase);

        /// <summary>
        /// The prefix reserved for neonKUBE related daemon, image, and pod labels.
        /// </summary>
        public const string ReservedLabelPrefix = "neonkube.io/";

        /// <summary>
        /// Parses and validates a YAML cluster definition file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="strict">Optionally require that all input properties map to <see cref="ClusterDefinition"/> properties.</param>
        /// <exception cref="ArgumentException">Thrown if the definition is not valid.</exception>
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
        /// <exception cref="ArgumentException">Thrown if the definition is not valid.</exception>
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
            return name != null && NameRegex.IsMatch(name);
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
        internal bool IsSpecialNeonCluster
        {
            get
            {
                switch (Hosting.Environment)
                {
                    case HostingEnvironment.HyperVLocal:

                        return Hosting.HyperVLocal != null && Hosting.HyperVLocal.NeonDesktopBuiltIn;

                    case HostingEnvironment.Wsl2:

                        return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Indicates whether the definition describes a neonDESKTOP built-in clusters.
        /// </summary>
        [JsonProperty(PropertyName = "IsDesktopCluster", Required = Required.Always)]
        [YamlMember(Alias = "isDesktopCluster", ApplyNamingConventions = false)]
        public bool IsDesktopCluster { get; set; }

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
        /// <para>
        /// The cluster domain. This will be used for accessing dashboards.
        /// </para>
        /// <note>
        /// The domain 
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Domain", Required = Required.Always)]
        [YamlMember(Alias = "domain", ApplyNamingConventions = false)]
        public string Domain { get; set; }

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
        /// as well as help originize you cost reporting.
        /// </para>
        /// <note>
        /// Currently, this is only supported for clusters deployed to AWS, Azure or Google Cloud.
        /// </note>
        /// </summary>
        public List<ResourceTag> ResourceTags { get; set; } = null;

        /// <summary>
        /// <para>
        /// Optionally specifies cluster debugging options.
        /// </para>
        /// <note>
        /// These options are generally intended for neonKUBE maintainers only.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Debug", Required = Required.Always)]
        [YamlMember(Alias = "debug", ApplyNamingConventions = false)]
        public DebugOptions Debug { get; set; } = new DebugOptions();

        /// <summary>
        /// Optionally specifies options used by <b>KubernetesFixture</b> and possibly
        /// custom tools for customizing cluster and node names to avoid conflicts.
        /// </summary>
        [JsonProperty(PropertyName = "Deployment", Required = Required.Always)]
        [YamlMember(Alias = "deployment", ApplyNamingConventions = false)]
        public DeploymentOptions Deployment { get; set; } = new DeploymentOptions();

        /// <summary>
        /// Specifies the cluster OpenEbs related options.
        /// </summary>
        [JsonProperty(PropertyName = "OpenEbs", Required = Required.Always)]
        [YamlMember(Alias = "openEbs", ApplyNamingConventions = false)]
        public OpenEbsOptions OpenEbs { get; set; } = new OpenEbsOptions();

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
        /// Returns the options to be used when installing Docker on each
        /// of the cluster nodes.
        /// </summary>
        [JsonProperty(PropertyName = "Docker", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "docker", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public DockerOptions Docker { get; set; } = new DockerOptions();

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
        /// Identifies the datacenter.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The name may include only letters, numbers, periods, dashes, and underscores.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "Datacenter", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "datacenter", ApplyNamingConventions = false)]
        [DefaultValue(defaultDatacenter)]
        public string Datacenter { get; set; } = defaultDatacenter;

        /// <summary>
        /// Indicates how the cluster is being used.
        /// </summary>
        [JsonProperty(PropertyName = "Environment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "environment", ApplyNamingConventions = false)]
        [JsonConverter(typeof(StringEnumConverter))]
        [DefaultValue(EnvironmentType.Other)]
        public EnvironmentType Environment { get; set; } = EnvironmentType.Other;

        /// <summary>
        /// Specifies the NTP time sources to be configured for the cluster.  These are the
        /// FQDNs or IP addresses of the sources.  This defaults to <b>pool.ntp.org</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The cluster masters will be configured to synchronize their time with these
        /// time sources and the worker nodes will be configured to synchronize their time
        /// with the master nodes.
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
        /// referencing the <b>apt-cacher-ng</b> instances running on the master nodes.
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
        /// Customizes the cluster's container registry configuration.
        /// </summary>
        [JsonProperty(PropertyName = "Registry", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "registry", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public RegistryOptions Registry { get; set; } = null;

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
                Hosting?.ClearSecrets(this);
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
        /// Enumerates the cluster master node definitions.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> Masters => Nodes.Where(n => n.IsMaster);

        /// <summary>
        /// Enumerates the cluster master node definitions sorted in ascending order by name.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> SortedMasterNodes => Masters.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase);

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
        /// Enumerates the cluster master nodes sorted by name follwed by the worker nodes,
        /// also sorted by name.  This is convienent for situations like assigning IP addresses
        /// or ports such that the masters are grouped together first.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> SortedMasterThenWorkerNodes => SortedMasterNodes.Union(SortedWorkerNodes);

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
                            Hosting.Aws.NodeSubnet,
                            $"{nameof(ClusterDefinition)}.{nameof(HostingOptions)}.{nameof(HostingOptions.Aws)}.{nameof(AwsHostingOptions.NodeSubnet)}", 
                            KubeConst.CloudSubnetStartReservedIPs, 
                            KubeConst.CloudSubnetEndReservedIPs);

                    case HostingEnvironment.Azure:

                        return new NodeSubnetInfo(
                            Hosting.Azure.NodeSubnet, 
                            $"{nameof(ClusterDefinition)}.{nameof(HostingOptions)}.{nameof(HostingOptions.Azure)}.{nameof(AzureHostingOptions.NodeSubnet)}",
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
        [Pure]
        public void ValidatePrivateNodeAddresses()
        {
            if (Hosting.Environment == HostingEnvironment.Wsl2)
            {
                // WSL2 nodes have dynamic IP addresses that change everytime the host
                // machine reboots so this check makes no sense for this environment.

                return;
            }

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
        [Pure]
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

            Debug       = Debug ?? new DebugOptions();
            Deployment  = Deployment ?? new DeploymentOptions();
            OpenEbs     = OpenEbs ?? new OpenEbsOptions();
            Security    = Security ?? new SecurityOptions();
            Kubernetes  = Kubernetes ?? new KubernetesOptions();
            Docker      = Docker ?? new DockerOptions();
            Monitor     = Monitor ?? new MonitorOptions();
            Hosting     = Hosting ?? new HostingOptions();
            NodeOptions = NodeOptions ?? new NodeOptions();
            Network     = Network ?? new NetworkOptions();
            Registry    = Registry ?? new RegistryOptions();

            if (IsDesktopCluster && Nodes.Count() > 1)
            {
                new ClusterDefinitionException($"[{nameof(IsDesktopCluster)}=true] is allowed only for single node clusters.");
            }

            if (IsDesktopCluster && !IsSpecialNeonCluster)
            {
                new ClusterDefinitionException($"[{nameof(IsDesktopCluster)}=true] is allowed only when [{nameof(IsSpecialNeonCluster)}=true].");
            }

            Debug.Validate(this);
            Deployment.Validate(this);
            OpenEbs.Validate(this);
            Security.Validate(this);
            Kubernetes.Validate(this);
            Docker.Validate(this);
            Monitor.Validate(this);
            Network.Validate(this);
            Hosting.Validate(this);
            NodeOptions.Validate(this);
            Network.Validate(this);
            Registry.Validate(this);

            new HostingManagerFactory().Validate(this);

            if (TimeSources == null || TimeSources.Length == 0 || TimeSources.Count(ts => string.IsNullOrWhiteSpace(ts)) > 0)
            {
                TimeSources = new string[] { "pool.ntp.org" };
            }

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
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Name)}] property is required.");
            }

            if (!IsValidName(Name))
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Name)}={Name}] property is not valid.  Only letters, numbers, periods, dashes, and underscores are allowed.");
            }

            if (Name.Length > 32)
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Name)}={Name}] has more than 32 characters.  Some hosting environments enforce name length limits so please trim your cluster name.");
            }

            if (Datacenter == null)
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Datacenter)}] property is required.");
            }

            if (!IsValidName(Datacenter))
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Datacenter)}={Datacenter}] property is not valid.  Only letters, numbers, periods, dashes, and underscores are allowed.");
            }

            var masterNodeCount = Masters.Count();

            if (masterNodeCount == 0)
            {
                throw new ClusterDefinitionException("Clusters must have at least one master node.");
            }
            else if (masterNodeCount > 5)
            {
                throw new ClusterDefinitionException("Clusters may not have more than [5] master nodes.");
            }
            else if (!NeonHelper.IsOdd(masterNodeCount))
            {
                throw new ClusterDefinitionException($"[{masterNodeCount}] master nodes is not allowed.  Only an odd number of master nodes is allowed: [1, 3, or 5]");
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
                        throw new ClusterDefinitionException($"Invalid IP address or HOSTNAME [{fields[0]}] in [{nameof(ClusterDefinition)}.{nameof(PackageProxy)}={PackageProxy}].");
                    }

                    if (!int.TryParse(fields[1], out var port) || !NetHelper.IsValidPort(port))
                    {
                        throw new ClusterDefinitionException($"Invalid port [{fields[1]}] in [{nameof(ClusterDefinition)}.{nameof(PackageProxy)}={PackageProxy}].");
                    }
                }
            }

            // Ensure that every node is assigned to an availability set, assigning master
            // nodes to the [master] set by default and worker nodes to the [worker] set.

            foreach (var node in Nodes)
            {
                if (!string.IsNullOrEmpty(node.Labels.PhysicalAvailabilitySet))
                {
                    continue;
                }

                node.Labels.PhysicalAvailabilitySet = node.IsMaster ? "master" : "worker";
            }
        }
    }
}

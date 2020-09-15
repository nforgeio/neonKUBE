//-----------------------------------------------------------------------------
// FILE:	    ClusterDefinition.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using Remotion.Linq.Parsing;

namespace Neon.Kube
{
    /// <summary>
    /// Describes a Kubernetes cluster.
    /// </summary>
    public class ClusterDefinition
    {
        //---------------------------------------------------------------------
        // Static members

        private const string        defaultDatacenter         = "DATACENTER";
        private const string        defaultProvisioner        = "unknown";
        private readonly string[]   defaultTimeSources        = new string[] { "pool.ntp.org" };
        private const int           defaultStepStaggerSeconds = 5;
        private const bool          defaultAllowUnitTesting   = false;
        private const string        defaultLinuxDistribution  = "ubuntu";
        private const string        defaultLinuxVersion       = "20.04.latest";

        /// <summary>
        /// Regex for verifying cluster names for hosts, routes, groups, etc.
        /// </summary>
        public static Regex NameRegex { get; private set; } = new Regex(@"^[a-z0-9.\-_]+$", RegexOptions.IgnoreCase);

        /// <summary>
        /// The prefix reserved for neonKUBE related daemon, image, and pod labels.
        /// </summary>
        public const string ReservedLabelPrefix = "neonkube.io/";

        /// <summary>
        /// Parses a cluster definition from YAML text.
        /// </summary>
        /// <param name="yaml">The JSON text.</param>
        /// <param name="strict">Optionally require that all input properties map to <see cref="ClusterDefinition"/> properties.</param>
        /// <returns>The parsed <see cref="ClusterDefinition"/>.</returns>
        /// <remarks>
        /// <note>
        /// The source is first preprocessed using <see cref="PreprocessReader"/>
        /// and then is parsed as YAML.
        /// </note>
        /// </remarks>
        public static ClusterDefinition FromYaml(string yaml, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(yaml != null, nameof(yaml));

            using (var stringReader = new StringReader(yaml))
            {
                using (var preprocessReader = new PreprocessReader(stringReader))
                {
                    var clusterDefinition = NeonHelper.YamlDeserialize<ClusterDefinition>(preprocessReader.ReadToEnd(), strict: strict);

                    clusterDefinition.Validate();

                    return clusterDefinition;
                }
            }
        }

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
                    using (var preprocessor = new PreprocessReader(reader))
                    {
                        preprocessor.ProcessStatements = true;

                        var clusterDefinition = NeonHelper.YamlDeserialize<ClusterDefinition>(preprocessor.ReadToEnd(), strict: strict);

                        if (clusterDefinition == null)
                        {
                            throw new ArgumentException($"Invalid cluster definition in [{path}].", nameof(path));
                        }

                        // Populate the [node.Name] properties from the dictionary name.

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

                        clusterDefinition.Validate();

                        return clusterDefinition;
                    }
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
        /// <returns>The size converted into a <c>long</c>.</returns>
        /// <exception cref="ClusterDefinitionException">Thrown if the size is not valid.</exception>
        public static long ValidateSize(string sizeValue, Type optionsType, string propertyName)
        {
            if (string.IsNullOrEmpty(sizeValue))
            {
                throw new ClusterDefinitionException($"[{optionsType.Name}.{propertyName}] cannot be NULL or empty.");
            }

            if (!ByteUnits.TryParse(sizeValue, out var size))
            {
                throw new ClusterDefinitionException($"[{optionsType.Name}.{propertyName}={sizeValue}] cannot be parsed.");
            }

            return (long)size;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterDefinition()
        {
        }

        /// <summary>
        /// <para>
        /// The cluster name.
        /// </para>
        /// <note>
        /// The name may include only letters, numbers, periods, dashes, and underscores and
        /// may be up to 20 characters long.  Some hosting environments enforce length limits
        /// on resource names that we derive from the cluster name, so please limit your
        /// cluster name to 20 characters.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        public string Name { get; set; }

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
        /// Specifies cluster debugging options.
        /// </para>
        /// <note>
        /// These options are generally intended for neonKUBE developers only.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Debug", Required = Required.Always)]
        [YamlMember(Alias = "debug", ApplyNamingConventions = false)]
        public DebugOptions Debug { get; set; } = new DebugOptions();

        /// <summary>
        /// Specifies cluster security options.
        /// </summary>
        [JsonProperty(PropertyName = "Security", Required = Required.Always)]
        [YamlMember(Alias = "security", ApplyNamingConventions = false)]
        public SecurityOptions Security { get; set; } = new SecurityOptions();

        /// <summary>
        /// Identifies the tool/version used to provision the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "Provisioner", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "provisioner", ApplyNamingConventions = false)]
        [DefaultValue(defaultProvisioner)]
        public string Provisioner { get; set; } = defaultProvisioner;

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
        /// Optionally enable unit testing on this cluster.  This is disabled by 
        /// default for safety.
        /// </summary>
        [JsonProperty(PropertyName = "AllowUnitTesting", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "allowUnitTesting", ApplyNamingConventions = false)]
        [DefaultValue(defaultAllowUnitTesting)]
        public bool AllowUnitTesting { get; set; } = defaultAllowUnitTesting;

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
        /// Specifies the distribution of Linux to be installed on the cluster nodes.  Currently
        /// only <b>ubuntu</b> is supported.  This defaults to <b>ubuntu</b>.
        /// </summary>
        [JsonProperty(PropertyName = "LinuxDistribution", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "linuxDistribution", ApplyNamingConventions = false)]
        [DefaultValue(defaultLinuxDistribution)]
        public string LinuxDistribution { get; set; } = defaultLinuxDistribution;

        /// <summary>
        /// <para>
        /// Specifies the version of <see cref="LinuxDistribution"/> to be installed.  This is
        /// formatted like <b>20.04.#</b> where <b>#</b> is the minor release or <b>20.04-latest</b>
        /// for the latest release.
        /// </para>
        /// <para>
        /// Currently, only <b>Ubuntu 20.04.#</b> releases are supported.  You'll need to check the
        /// cluster install documentation to discover which point releases are currently available.
        /// </para>
        /// <para>
        /// This defaults to <b>20.04.latest</b>.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "LinuxVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "linuxVersion", ApplyNamingConventions = false)]
        [DefaultValue(defaultLinuxVersion)]
        public string LinuxVersion { get; set; } = defaultLinuxVersion;

        /// <summary>
        /// <para>
        /// Optionally overrides the location of the Linux node template URI.  This is usually
        /// located on a neonFORGE managed server and derived from <see cref="LinuxDistribution"/>
        /// and <see cref="LinuxVersion"/> which should work for most users.
        /// </para>
        /// <para>
        /// You may set this to a custom URI which may be useful for setting up air-gapped 
        /// clusters for for testing purposes.  This defaults to <c>null</c>.
        /// </para>
        /// <note>
        /// This URI can use HTTP, HTTPS, or FTP for all hosting environments except <see cref="HostingEnvironment.XenServer"/>
        /// which doesn't support HTTPS.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "LinuxTemplateUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "linuxTemplateUri", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string LinuxTemplateUri { get; set; } = null;

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
        /// Optionally specifies setup process related options.
        /// </summary>
        [JsonProperty(PropertyName = "Setup", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "setup", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public SetupOptions Setup { get; set; } = null;

        /// <summary>
        /// Describes the cluster's network configuration.
        /// </summary>
        [JsonProperty(PropertyName = "Network", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "network", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public NetworkOptions Network { get; set; } = new NetworkOptions();

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
        /// Validates that node private IP addresses are set, are within the nodes subnet, and
        /// are unique.  This method is intended to be called from hosting options classes
        /// like <see cref="MachineHostingOptions"/> which require specified node IP addresses.
        /// </summary>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void ValidatePrivateNodeAddresses()
        {
            var ipAddressToNode = new Dictionary<IPAddress, NodeDefinition>();

            if (string.IsNullOrEmpty(Network.NodeSubnet))
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(ClusterDefinition.Network)}.{nameof(NetworkOptions.NodeSubnet)}] property is required.");
            }

            if (!NetworkCidr.TryParse(Network.NodeSubnet, out var nodeSubnet))
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(ClusterDefinition.Network)}.{nameof(NetworkOptions.NodeSubnet)}={Network.NodeSubnet}] property is not valid.");
            }

            foreach (var node in SortedNodes.OrderBy(n => n.Name))
            {
                if (string.IsNullOrEmpty(node.Address))
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] has not been assigned a private IP address.");
                }

                if (!IPAddress.TryParse(node.Address, out var address))
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] has invalid private IP address [{node.Address}].");
                }

                if (address == IPAddress.Any)
                {
                    throw new ClusterDefinitionException($"Node [{node.Name}] has not been assigned a private IP address.");
                }

                if (ipAddressToNode.TryGetValue(address, out var conflictingNode))
                {
                    throw new ClusterDefinitionException($"Nodes [{conflictingNode.Name}] and [{node.Name}] have the same IP address [{address}].");
                }

                ipAddressToNode.Add(address, node);
            }
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
            Security    = Security ?? new SecurityOptions();
            Provisioner = Provisioner ?? defaultProvisioner;
            Kubernetes  = Kubernetes ?? new KubernetesOptions();
            Docker      = Docker ?? new DockerOptions();
            Monitor     = Monitor ?? new MonitorOptions();
            Setup       = Setup ?? new SetupOptions();
            Hosting     = Hosting ?? new HostingOptions();
            NodeOptions = NodeOptions ?? new NodeOptions();
            Network     = Network ?? new NetworkOptions();

            Debug.Validate(this);
            Security.Validate(this);
            Kubernetes.Validate(this);
            Docker.Validate(this);
            Monitor.Validate(this);
            Setup.Validate(this);
            Network.Validate(this);
            Hosting.Validate(this);
            NodeOptions.Validate(this);
            Network.Validate(this);

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

            if (!string.IsNullOrEmpty(ClusterVersion))
            {
                if (!SemanticVersion.TryParse(ClusterVersion, out var clusterVer))
                {
                    throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(ClusterVersion)}={ClusterVersion}] is not a valid semantic version.");
                }

                if (!KubeConst.SupportedClusterVersions.Any(v => v.Equals(ClusterVersion, StringComparison.InvariantCultureIgnoreCase)))
                {
                    throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(ClusterVersion)}={ClusterVersion}] is not a supported cluster version.");
                }
            }
            else
            {
                ClusterVersion = KubeConst.LatestClusterVersion;
            }

            if (Name == null)
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Name)}] property is required.");
            }

            if (!IsValidName(Name))
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Name)}={Name}] property is not valid.  Only letters, numbers, periods, dashes, and underscores are allowed.");
            }

            if (Name.Length > 20)
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Name)}={Name}] has more than 20 characters.  Some hosting environments enforce name length limits so please trim your cluster name.");
            }

            if (Datacenter == null)
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Datacenter)}] property is required.");
            }

            if (!IsValidName(Datacenter))
            {
                throw new ClusterDefinitionException($"The [{nameof(ClusterDefinition)}.{nameof(Datacenter)}={Datacenter}] property is not valid.  Only letters, numbers, periods, dashes, and underscores are allowed.");
            }

            // Validate the Linux distribution.

            var distribution = LinuxDistribution ?? defaultLinuxDistribution;

            switch (distribution)
            {
                // Supported distributions

                case "ubuntu":

                    break;

                default:

                    throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(LinuxDistribution)}={distribution}] is not one of the supported distributions: ubuntu.");
            }

            // Validate the Linux version.  This needs to look like one of:
            //
            //      #.#.#
            //      #.#.latest

            var version = LinuxVersion ?? defaultLinuxVersion;
            var versionFields = version.Split('.');
            int v;

            if (versionFields.Length != 3 ||
                !int.TryParse(versionFields[0], out v) ||
                !int.TryParse(versionFields[1], out v) ||
                (!int.TryParse(versionFields[2], out v) && versionFields[2] != "latest"))
            {
                throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(LinuxVersion)}={version}] is not a valid Linux distribution version.");
            }

            // Validate the optional override VM template URI.

            if (!string.IsNullOrEmpty(LinuxTemplateUri))
            {
                if (!Uri.TryCreate(LinuxTemplateUri, UriKind.Absolute, out var uri))
                {
                    throw new ClusterDefinitionException($"[{nameof(HostingOptions)}.{nameof(LinuxTemplateUri)}={LinuxTemplateUri}] is not a valid URI.");
                }
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
                throw new ClusterDefinitionException($"[{masterNodeCount}] master nodes is not allowed.  Only an off number of master nodes is allowed: [1, 3, or 5]");
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

                    if (!IPAddress.TryParse(fields[0], out var address) && !NetHelper.IsValidHost(fields[0]))
                    {
                        throw new ClusterDefinitionException($"Invalid IP address or HOSTNAME [{fields[0]}] in [{nameof(ClusterDefinition)}.{nameof(PackageProxy)}={PackageProxy}].");
                    }

                    if (!int.TryParse(fields[1], out var port) || !NetHelper.IsValidPort(port))
                    {
                        throw new ClusterDefinitionException($"Invalid port [{fields[1]}] in [{nameof(ClusterDefinition)}.{nameof(PackageProxy)}={PackageProxy}].");
                    }
                }
            }

            // Ensure that each node has a valid unique or NULL IP address.

            NetworkCidr nodeSubnet = null;

            if (Network.NodeSubnet != null)
            {
                nodeSubnet = NetworkCidr.Parse(Network.NodeSubnet);
            }

            var addressToNode = new Dictionary<string, NodeDefinition>();

            foreach (var node in SortedNodes)
            {
                if (node.Address != null)
                {
                    NodeDefinition conflictNode;

                    if (addressToNode.TryGetValue(node.Address, out conflictNode))
                    {
                        throw new ClusterDefinitionException($"Node [name={node.Name}] has invalid private IP address [{node.Address}] that conflicts with node [name={conflictNode.Name}].");
                    }
                }
            }

            foreach (var node in SortedNodes)
            {
                if (node.Address != null)
                {
                    if (!IPAddress.TryParse(node.Address, out var address))
                    {
                        throw new ClusterDefinitionException($"Node [name={node.Name}] has invalid private IP address [{node.Address}].");
                    }

                    if (nodeSubnet != null && !nodeSubnet.Contains(address))
                    {
                        throw new ClusterDefinitionException($"Node [name={node.Name}] has private IP address [{node.Address}] that is not within the hosting [{nameof(Network.NodeSubnet)}={Network.NodeSubnet}].");
                    }
                }
                else if (!Hosting.IsCloudProvider)
                {
                    throw new ClusterDefinitionException($"Node [name={node.Name}] is not assigned a private IP address.  This is required when deploying to a [{nameof(Environment)}={Environment}] hosting environment.");
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
    }
}

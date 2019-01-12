//-----------------------------------------------------------------------------
// FILE:	    KubeDefinition.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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
    /// Describes a cluster.
    /// </summary>
    public class KubeDefinition
    {
        //---------------------------------------------------------------------
        // Static members

        private const string        defaultDatacenter = "DATACENTER";
        private const string        defaultProvisioner = "unknown";
        private readonly string[]   defaultTimeSources = new string[] { "pool.ntp.org" };
        private const string        defaultDrivePrefix = "sd";
        private const int           defaultStepStaggerSeconds = 5;
        private const bool          defaultAllowUnitTesting = false;

        /// <summary>
        /// Regex for verifying cluster names for hosts, routes, groups, etc.
        /// </summary>
        public static Regex NameRegex { get; private set; } = new Regex(@"^[a-z0-9.\-_]+$", RegexOptions.IgnoreCase);

        /// <summary>
        /// Regex for verifying DNS hostnames.
        /// </summary>
        public static Regex DnsHostRegex { get; private set; } = new Regex(@"^([a-z0-9]|[a-z0-9][a-z0-9\-_]{0,61}[a-z0-9])(\.([a-z0-9]|[a-z0-9][a-z0-9\-_]{0,61}[a-z0-9_]))*$", RegexOptions.IgnoreCase);

        /// <summary>
        /// The prefix reserved for cluster related Docker daemon, image, and container labels.
        /// </summary>
        public const string ReservedLabelPrefix = "io.neonkube";

        /// <summary>
        /// Parses a cluster definition from JSON text.
        /// </summary>
        /// <param name="json">The JSON text.</param>
        /// <param name="strict">Optionally require that all input properties map to <see cref="KubeDefinition"/> properties.</param>
        /// <returns>The parsed <see cref="KubeDefinition"/>.</returns>
        /// <remarks>
        /// <note>
        /// The source is first preprocessed using <see cref="PreprocessReader"/>
        /// and then is parsed as JSON.
        /// </note>
        /// </remarks>
        public static KubeDefinition FromJson(string json, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(json != null);

            using (var stringReader = new StringReader(json))
            {
                using (var preprocessReader = new PreprocessReader(stringReader))
                {
                    return NeonHelper.JsonDeserialize<KubeDefinition>(preprocessReader.ReadToEnd(), strict: strict);
                }
            }
        }

        /// <summary>
        /// Parses and validates a cluster definition file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="strict">Optionally require that all input properties map to <see cref="KubeDefinition"/> properties.</param>
        /// <exception cref="ArgumentException">Thrown if the definition is not valid.</exception>
        public static void ValidateFile(string path, bool strict = false)
        {
            FromFile(path, strict: strict);
        }

        /// <summary>
        /// Parses a cluster definition from a file.
        /// </summary>
        /// <param name="path">The file path.</param>
        /// <param name="strict">Optionally require that all input properties map to <see cref="KubeDefinition"/> properties.</param>
        /// <returns>The parsed <see cref="KubeDefinition"/>.</returns>
        /// <exception cref="ArgumentException">Thrown if the definition is not valid.</exception>
        /// <remarks>
        /// <note>
        /// The source is first preprocessed using <see cref="PreprocessReader"/>
        /// and then is parsed as JSON.
        /// </note>
        /// </remarks>
        public static KubeDefinition FromFile(string path, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(path != null);

            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                using (var stringReader = new StreamReader(stream))
                {
                    using (var preprocessReader = new PreprocessReader(stringReader))
                    {
                        var kubeDefinition = NeonHelper.JsonDeserialize<KubeDefinition>(preprocessReader.ReadToEnd(), strict: strict);

                        if (kubeDefinition == null)
                        {
                            throw new ArgumentException($"Invalid cluster definition in [{path}].");
                        }

                        // Populate the [node.Name] properties from the dictionary name.

                        foreach (var item in kubeDefinition.NodeDefinitions)
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

                        kubeDefinition.Validate();

                        return kubeDefinition;
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
        /// <exception cref="KubeDefinitionException">Thrown if the size is not valid.</exception>
        public static long ValidateSize(string sizeValue, Type optionsType, string propertyName)
        {
            if (string.IsNullOrEmpty(sizeValue))
            {
                throw new KubeDefinitionException($"[{optionsType.Name}.{propertyName}] cannot be NULL or empty.");
            }

            if (!NeonHelper.TryParseCount(sizeValue, out var size))
            {
                throw new KubeDefinitionException($"[{optionsType.Name}.{propertyName}={sizeValue}] cannot be parsed.");
            }

            return (long)size;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeDefinition()
        {
        }

        /// <summary>
        /// The cluster name.
        /// </summary>
        /// <remarks>
        /// <note>
        /// The name may include only letters, numbers, periods, dashes, and underscores.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        public string Name { get; set; }

        /// <summary>
        /// Identifies the tool/version used to provision the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "Provisioner", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultProvisioner)]
        public string Provisioner { get; set; } = defaultProvisioner;

        /// <summary>
        /// <para>
        /// Returns the prefix for block devices that will be attached to
        /// the host machines.  For many hosting environments this will be
        /// <b>sd</b>, indicating that drives will be attached like: 
        /// <b>/dev/sda</b>, <b>/dev/sdb</b>, <b>/dev/sdc</b>...
        /// </para>
        /// <para>
        /// This may be different though for some hosting environment.
        /// XenServer for example, uses the <b>xvd</b> prefix and attaches
        /// drives as <b>/dev/sda</b>, <b>/dev/sdb</b>, <b>/dev/sdc</b>...
        /// </para>
        /// <note>
        /// This property is set automatically during cluster provisioning.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "DrivePrefix", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultDrivePrefix)]
        public string DrivePrefix { get; set; } = defaultDrivePrefix;

        /// <summary>
        /// Optionally enable unit testing on this cluster.  This is disabled by 
        /// default for safety.
        /// </summary>
        [JsonProperty(PropertyName = "AllowUnitTesting", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultAllowUnitTesting)]
        public bool AllowUnitTesting { get; set; } = defaultAllowUnitTesting;

        /// <summary>
        /// Specifies hosting related settings (e.g. the cloud provider).  This defaults to
        /// <c>null</c> which indicates that the cluster will be hosted on private servers.
        /// </summary>
        [JsonProperty(PropertyName = "Hosting", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
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
        [DefaultValue(defaultDatacenter)]
        public string Datacenter { get; set; } = defaultDatacenter;

        /// <summary>
        /// Indicates how the cluster is being used.
        /// </summary>
        [JsonProperty(PropertyName = "Environment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [JsonConverter(typeof(StringEnumConverter))]
        [DefaultValue(EnvironmentType.Other)]
        public EnvironmentType Environment { get; set; } = EnvironmentType.Other;

        /// <summary>
        /// Specifies the NTP time sources to be configured for the cluster.  These are the
        /// FQDNs or IP addresses of the sources.  This defaults to <b>pool.ntp.org</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The cluster managers will be configured to synchronize their time with these
        /// time sources and the worker nodes will be configured to synchronize their time
        /// with the manager nodes.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "TimeSources", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string[] TimeSources { get; set; } = null;

        /// <summary>
        /// Optionally specifies setup process related options.
        /// </summary>
        [JsonProperty(PropertyName = "Setup", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public SetupOptions Setup { get; set; } = null;

        /// <summary>
        /// Specifies host node options.
        /// </summary>
        [JsonProperty(PropertyName = "HiveNode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public KubeNodeOptions NodeOptions { get; set; } = new KubeNodeOptions();

        /// <summary>
        /// Describes the cluster's network configuration.
        /// </summary>
        [JsonProperty(PropertyName = "Network", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public NetworkOptions Network { get; set; } = new NetworkOptions();

        /// <summary>
        /// Describes the Docker host nodes in the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "Nodes", Required = Required.Always)]
        public Dictionary<string, NodeDefinition> NodeDefinitions { get; set; } = new Dictionary<string, NodeDefinition>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// <para>
        /// Set to the MD5 hash (encoded as base64) of the cluster definition for scenarios
        /// where its necessary to quickly determine whether two definitions are the same.
        /// This is computed by calling <see cref="ComputeHash()"/>
        /// </para>
        /// <note>
        /// The computed hash does not include the hosting provider details because these
        /// typically include hosting related secrets and so they are not persisted to
        /// the cluster Consul service.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Hash", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.Include)]
        [DefaultValue(null)]
        public string Hash { get; set; }

        /// <summary>
        /// Enumerates all cluster node definitions.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> Nodes
        {
            get { return NodeDefinitions.Values; }
        }

        /// <summary>
        /// Enumerates all cluster node definitions sorted in ascending order by name.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> SortedNodes
        {
            get { return Nodes.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Enumerates the cluster manager node definitions.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> Managers
        {
            get { return Nodes.Where(n => n.IsManager); }
        }

        /// <summary>
        /// Enumerates the cluster manager node definitions sorted in ascending order by name.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> SortedManagers
        {
            get { return Managers.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Enumerates the cluster worker node definitions.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> Workers
        {
            get { return Nodes.Where(n => n.IsWorker); }
        }

        /// <summary>
        /// Enumerates the cluster worker node definitions sorted in ascending order by name.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> SortedWorkers
        {
            get { return Workers.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Enumerates the cluster swarm node definitions (the managers and workers).
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> Swarm
        {
            get { return Nodes.Where(n => n.InSwarm); }
        }

        /// <summary>
        /// Enumerates the cluster swarm node definitions (the managers and workers)
        /// sorted in ascending order by name.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public IEnumerable<NodeDefinition> SortedSwarm
        {
            get { return Swarm.OrderBy(n => n.Name, StringComparer.OrdinalIgnoreCase); }
        }

        /// <summary>
        /// Validates that node private IP addresses are set, are within the nodes subnet, and
        /// are unique.  This method is intended to be called from hosting options classes
        /// like <see cref="MachineOptions"/> which require specified node IP addresses.
        /// </summary>
        /// <exception cref="KubeDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void ValidatePrivateNodeAddresses()
        {
            var ipAddressToNode = new Dictionary<IPAddress, NodeDefinition>();

            if (string.IsNullOrEmpty(Network.NodesSubnet))
            {
                throw new KubeDefinitionException($"The [{nameof(KubeDefinition)}.{nameof(KubeDefinition.Network)}.{nameof(NetworkOptions.NodesSubnet)}] property is required.");
            }

            if (!NetworkCidr.TryParse(Network.NodesSubnet, out var nodesSubnet))
            {
                throw new KubeDefinitionException($"The [{nameof(KubeDefinition)}.{nameof(KubeDefinition.Network)}.{nameof(NetworkOptions.NodesSubnet)}={Network.NodesSubnet}] property is not valid.");
            }

            foreach (var node in SortedNodes.OrderBy(n => n.Name))
            {
                if (string.IsNullOrEmpty(node.PrivateAddress))
                {
                    throw new KubeDefinitionException($"Node [{node.Name}] has not been assigned a private IP address.");
                }

                if (!IPAddress.TryParse(node.PrivateAddress, out var address))
                {
                    throw new KubeDefinitionException($"Node [{node.Name}] has invalid private IP address [{node.PrivateAddress}].");
                }

                if (address == IPAddress.Any)
                {
                    throw new KubeDefinitionException($"Node [{node.Name}] has not been assigned a private IP address.");
                }

                if (ipAddressToNode.TryGetValue(address, out var conflictingNode))
                {
                    throw new KubeDefinitionException($"Nodes [{conflictingNode.Name}] and [{node.Name}] have the same IP address [{address}].");
                }

                ipAddressToNode.Add(address, node);
            }
        }

        /// <summary>
        /// Validates the cluster definition and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <exception cref="KubeDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate()
        {
            Provisioner = Provisioner ?? defaultProvisioner;
            DrivePrefix = DrivePrefix ?? defaultDrivePrefix;
            Setup       = Setup ?? new SetupOptions();
            Hosting     = Hosting ?? new HostingOptions();
            NodeOptions = NodeOptions ?? new KubeNodeOptions();
            Network     = Network ?? new NetworkOptions();

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
                throw new KubeDefinitionException("At least one cluster node must be defined.");
            }

            foreach (var node in NodeDefinitions.Values)
            {
                node.Validate(this);
            }

            if (Name == null)
            {
                throw new KubeDefinitionException($"The [{nameof(KubeDefinition)}.{nameof(Name)}] property is required.");
            }

            if (!IsValidName(Name))
            {
                throw new KubeDefinitionException($"The [{nameof(KubeDefinition)}.{nameof(Name)}={Name}] property is not valid.  Only letters, numbers, periods, dashes, and underscores are allowed.");
            }

            if (Datacenter == null)
            {
                throw new KubeDefinitionException($"The [{nameof(KubeDefinition)}.{nameof(Datacenter)}] property is required.");
            }

            if (!IsValidName(Datacenter))
            {
                throw new KubeDefinitionException($"The [{nameof(KubeDefinition)}.{nameof(Datacenter)}={Datacenter}] property is not valid.  Only letters, numbers, periods, dashes, and underscores are allowed.");
            }

            var managementNodeCount = Managers.Count();

            if (managementNodeCount == 0)
            {
                throw new KubeDefinitionException("Hives must have at least one management node.");
            }
            else if (managementNodeCount > 5)
            {
                throw new KubeDefinitionException("Hives may not have more than [5] management nodes.");
            }
            else if (!NeonHelper.IsOdd(managementNodeCount))
            {
                throw new KubeDefinitionException("Hives must have an odd number of management nodes: [1, 3, or 5]");
            }

            // Ensure that each node has a valid unique or NULL IP address.

            NetworkCidr nodesSubnet   = null;
            NetworkCidr vpnPoolSubnet = null;

            if (Network.NodesSubnet != null)
            {
                nodesSubnet = NetworkCidr.Parse(Network.NodesSubnet);
            }

            var addressToNode = new Dictionary<string, NodeDefinition>();

            foreach (var node in SortedNodes)
            {
                if (node.PrivateAddress != null)
                {
                    NodeDefinition conflictNode;

                    if (addressToNode.TryGetValue(node.PrivateAddress, out conflictNode))
                    {
                        throw new KubeDefinitionException($"Node [name={node.Name}] has invalid private IP address [{node.PrivateAddress}] that conflicts with node [name={conflictNode.Name}].");
                    }
                }
            }

            foreach (var node in SortedNodes)
            {
                if (node.PrivateAddress != null)
                {
                    if (!IPAddress.TryParse(node.PrivateAddress, out var address))
                    {
                        throw new KubeDefinitionException($"Node [name={node.Name}] has invalid private IP address [{node.PrivateAddress}].");
                    }

                    if (vpnPoolSubnet != null && vpnPoolSubnet.Contains(address))
                    {
                        throw new KubeDefinitionException($"Node [name={node.Name}] has private IP address [{node.PrivateAddress}] within the hosting [{nameof(Network.VpnPoolSubnet)}={Network.VpnPoolSubnet}].");
                    }

                    if (nodesSubnet != null && !nodesSubnet.Contains(address))
                    {
                        throw new KubeDefinitionException($"Node [name={node.Name}] has private IP address [{node.PrivateAddress}] that is not within the hosting [{nameof(Network.NodesSubnet)}={Network.NodesSubnet}].");
                    }
                }
                else if (!Hosting.IsCloudProvider)
                {
                    throw new KubeDefinitionException($"Node [name={node.Name}] is not assigned a private IP address.  This is required when deploying to a [{nameof(Environment)}={Environment}] hosting environment.");
                }
            }
        }

        /// <summary>
        /// Adds a docker node to the cluster.
        /// </summary>
        /// <param name="node">The new node.</param>
        public void AddNode(NodeDefinition node)
        {
            Covenant.Requires<ArgumentNullException>(node != null);
            Covenant.Requires<ArgumentException>(NeonHelper.DoesNotThrow(() => node.Validate(this)));

            NodeDefinitions.Add(node.Name, node);
        }

        /// <summary>
        /// Computes the <see cref="Hash"/> property value.
        /// </summary>
        public void ComputeHash()
        {
            // We're going to create a deep clone of the current instance
            // and then clear it's Hash property as well as any hosting
            // provider details.

            var clone = NeonHelper.JsonClone<KubeDefinition>(this);

            clone.Hash = null;

            // Don't include any hosting related secrets in the clone.

            clone.Hosting?.ClearSecrets();

            // We need to ensure that JSON.NET serializes the nodes in a consistent
            // order (e.g. ascending order by name) so we'll compute the same hash
            // for two definitions with different orderings.
            //
            // We'll accomplish this by rebuilding the cloned node definitions in
            // ascending order.

            var nodes = clone.NodeDefinitions;

            clone.NodeDefinitions = new Dictionary<string, NodeDefinition>();

            foreach (var nodeName in nodes.Keys.OrderBy(n => n))
            {
                clone.NodeDefinitions.Add(nodeName, nodes[nodeName]);
            }

            // Compute the hash.

            this.Hash = MD5.Create().ComputeHashBase64(NeonHelper.JsonSerialize(clone));
        }

        /// <summary>
        /// Filters the cluster swarm nodes by applying the zero or more Docker Swarm style constraints.
        /// </summary>
        /// <param name="constraints">The constraints.</param>
        /// <returns>The set of swarm nodes that satisfy <b>all</b> of the constraints.</returns>
        /// <remarks>
        /// <note>
        /// All of the swarm nodes will be returned if the parameter is <c>null</c> or empty.
        /// </note>
        /// <para>
        /// Constraint expressions must take the form of <b>LABEL==VALUE</b> or <b>LABEL!=VALUE</b>.
        /// This method will do a case insensitive comparision the node label with the
        /// value specified.
        /// </para>
        /// <para>
        /// Properties may be custom label names, cluster label names prefixed with <b>io.neonkube.</b>,
        /// or <b>node</b> to indicate the node name.  Label name lookup is case insenstive.
        /// </para>
        /// </remarks>
        public IEnumerable<NodeDefinition> FilterSwarmNodes(IEnumerable<string> constraints)
        {
            var filtered = this.SortedSwarm.ToList();

            if (constraints == null || constraints.FirstOrDefault() == null)
            {
                return filtered;
            }

            var labelDictionary = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var node in filtered)
            {
                var labels = new Dictionary<string, string>();

                labels.Add("node", node.Name);

                foreach (var label in node.Labels.Standard)
                {
                    labels.Add(label.Key, label.Value.ToString());
                }

                foreach (var label in node.Labels.Custom)
                {
                    labels.Add(label.Key, label.Value.ToString());
                }

                labelDictionary.Add(node.Name, labels);
            }

            foreach (var constraint in constraints)
            {
                if (string.IsNullOrWhiteSpace(constraint))
                {
                    continue;
                }

                var matches = new List<NodeDefinition>();

                foreach (var worker in filtered)
                {
                    var pos      = constraint.IndexOf("==");
                    var equality = true;

                    if (pos < 0)
                    {
                        pos = constraint.IndexOf("!=");

                        if (pos < 0)
                        {
                            throw new KubeDefinitionException($"Illegal constraint [{constraint}].  One of [==] or [!=] must be present.");
                        }

                        equality = false;
                    }

                    if (pos == 0)
                    {
                        throw new KubeDefinitionException($"Illegal constraint [{constraint}].  No label is specified.");
                    }

                    string  label = constraint.Substring(0, pos);
                    string  value = constraint.Substring(pos + 2);
                    string  nodeValue;

                    if (!labelDictionary[worker.Name].TryGetValue(label, out nodeValue))
                    {
                        nodeValue = string.Empty;
                    }

                    var equals = nodeValue.Equals(value, StringComparison.OrdinalIgnoreCase);

                    if (equality == equals)
                    {
                        matches.Add(worker);
                    }
                }

                filtered = matches;

                if (filtered.Count == 0)
                {
                    return filtered;
                }
            }

            return filtered;
        }
    }
}

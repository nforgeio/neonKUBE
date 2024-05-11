//-----------------------------------------------------------------------------
// FILE:        ClusterTelemetry.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Kube.ClusterDef;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Models cluster telemetry transmitted periodically to the headend.
    /// </summary>
    public class ClusterTelemetry
    {
        private const string schema = "1";

        /// <summary>
        /// Constructor
        /// </summary>
        public ClusterTelemetry() 
        {
        }

        /// <summary>
        /// Specifies the telemetry schema version.
        /// </summary>
        [JsonProperty(PropertyName = "Schema", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(schema)]
        public string Schema { get; set; } = schema;

        /// <summary>
        /// Receive timestamp.  This isn't transmitted by the cluster and is set by the headend
        /// when it receives cluster telemetry pings.
        /// </summary>
        [JsonProperty(PropertyName = "Timestamp", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Timestamp { get; set; }

        /// <summary>
        /// Cluster details.
        /// </summary>
        [JsonProperty(PropertyName = "Details", Required = Required.Always | Required.AllowNull)]
        public ClusterDetails Details { get; set; }

        /// <summary>
        /// Node status information.
        /// </summary>
        [JsonProperty(PropertyName = "Nodes", Required = Required.Always | Required.AllowNull)]
        public List<ClusterNodeTelemetry> Nodes { get; set; } = new List<ClusterNodeTelemetry>();
    }

    /// <summary>
    /// Holds relevant cluster details.
    /// </summary>
    public class ClusterDetails
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterDetails()
        {
        }

        /// <summary>
        /// Constructs an instance by extracting relevant properties from the
        /// <see cref="ClusterInfo"/> passed.
        /// </summary>
        /// <param name="clusterInfo">Specifies the cluster information.</param>
        public ClusterDetails(ClusterInfo clusterInfo)
        {
            Covenant.Requires<ArgumentNullException>(clusterInfo != null, nameof(clusterInfo));

            this.ClusterId          = clusterInfo.ClusterId;
            this.ClusterVersion     = clusterInfo.ClusterVersion;
            this.CreationTimestamp  = clusterInfo.CreationTimestamp;
            this.Datacenter         = clusterInfo.Datacenter;
            this.Domain             = clusterInfo.Domain;
            this.FeatureOptions     = clusterInfo.FeatureOptions;
            this.HostingEnvironment = clusterInfo.HostingEnvironment;
            this.IsDesktop          = clusterInfo.IsDesktop;
            this.KubernetesVersion  = clusterInfo.KubernetesVersion;
            this.Latitude           = clusterInfo.Latitude;
            this.Longitude          = clusterInfo.Longitude;
            this.Name               = clusterInfo.Name;
            this.OrganizationId     = clusterInfo.OrganizationId;
            this.PublicAddresses    = clusterInfo.PublicAddresses;
            this.Purpose            = clusterInfo.Purpose;
            this.Summary            = clusterInfo.Summary;
        }

        /// <summary>
        /// Globally unique cluster identifier.  This is set during cluster setup and is 
        /// used to distinguish between customer clusters.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ClusterId { get; set; } = null;

        /// <summary>
        /// The NeonKUBE version of the cluster.  This is formatted as a <see cref="SemanticVersion"/>.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(KubeVersion.NeonKube)]
        public string ClusterVersion { get; set; } = KubeVersion.NeonKube;

        /// <summary>
        /// Timestamp representing the date that the cluster was created.
        /// </summary>
        [JsonProperty(PropertyName = "CreationTimestamp", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public DateTime? CreationTimestamp { get; set; }

        /// <summary>
        /// Identifies where the cluster is hosted as specified by <see cref="ClusterDefinition.Datacenter"/> in the cluster
        /// definition.  That property defaults to the empty string for on-premise clusters and the the region for cloud
        /// based clusters.
        /// </summary>
        [JsonProperty(PropertyName = "Datacenter", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("")]
        public string Datacenter { get; set; } = string.Empty;

        /// <summary>
        /// Identifies the DNS domain assigned to the cluster when it was provisioned.
        /// </summary>
        [JsonProperty(PropertyName = "Domain", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("")]
        public string Domain { get; set; } = string.Empty;

        /// <summary>
        /// Describes which optional components have been deployed to the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "FeatureOptions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public FeatureOptions FeatureOptions { get; set; } = new FeatureOptions();

        /// <summary>
        /// Identifies the cloud or other hosting platform.
        /// definition. 
        /// </summary>
        [JsonProperty(PropertyName = "HostingEnvironment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(HostingEnvironment.Unknown)]
        public HostingEnvironment HostingEnvironment { get; set; } = HostingEnvironment.Unknown;

        /// <summary>
        /// Indicates whether the cluster is a neon-desktop cluster.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "IsDesktop", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool IsDesktop { get; set; } = false;

        /// <summary>
        /// Identifies the version of Kubernetes installed on the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "KubernetesVersion", Required = Required.Always)]
        public string KubernetesVersion { get; set; }

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
        [JsonProperty(PropertyName = "Latitude", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
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
        [JsonProperty(PropertyName = "Longitude", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public double? Longitude { get; set; } = null;

        /// <summary>
        /// Identifies the cluster by name as specified by <see cref="ClusterDefinition.Name"/> in the cluster definition.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Identifies the organization that owns the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "OrganizationId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string OrganizationId { get; set; } = null;

        /// <summary>
        /// <para>
        /// Lists the IP addresses that can be used to communicate with the cluster.
        /// </para>
        /// <para>
        /// For cloud deployed clusters, this will be configured by default with the public IP
        /// address assigned to the cluster load balancer.  For on-premis clusters, this will
        /// be set to the IP addresses of the control-plane nodes by default.
        /// </para>
        /// <para>
        /// Users may also customize this by setting IP addresses in the cluster definition.
        /// This is often done for clusters behind a router mapping the public IP address
        /// to the LAN address for cluster nodes.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "PublicAddresses", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> PublicAddresses { get; set; } = null;

        /// <summary>
        /// Indicates how the cluster is being used as specified by <see cref="ClusterDefinition.Purpose"/>.
        /// definition. 
        /// </summary>
        [JsonProperty(PropertyName = "Purpose", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(ClusterPurpose.Unspecified)]
        public ClusterPurpose Purpose { get; set; } = ClusterPurpose.Unspecified;

        /// <summary>
        /// Human readable string that summarizes the cluster state.
        /// </summary>
        [JsonProperty(PropertyName = "Summary", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Summary { get; set; } = null;
    }

    /// <summary>
    /// Node Telemetry
    /// </summary>
    public class ClusterNodeTelemetry
    {
        /// <summary>
        /// Identifies the node role
        /// </summary>
        [JsonProperty(PropertyName = "Role", Required = Required.Always | Required.AllowNull)]
        public string Role { get; set; }

        /// <summary>
        /// Identifies the CPU architecture.
        /// </summary>
        [JsonProperty(PropertyName = "CpuArchitecture", Required = Required.Always | Required.AllowNull)]
        public string CpuArchitecture { get; set; }
        
        /// <summary>
        /// Reports number of node vCPUs.
        /// </summary>
        [JsonProperty(PropertyName = "VCpus", Required = Required.Always | Required.AllowNull)]
        public int VCpus { get; set; }

        /// <summary>
        /// The memory available.
        /// </summary>
        [JsonProperty(PropertyName = "Memory", Required = Required.Always | Required.AllowNull)]
        public string Memory { get; set; }

        /// <summary>
        /// Identifies the node kernel version.
        /// </summary>
        [JsonProperty(PropertyName = "KernelVersion", Required = Required.Always | Required.AllowNull)]
        public string KernelVersion { get; set; }

        /// <summary>
        /// Identifies the node operation system.
        /// </summary>
        [JsonProperty(PropertyName = "OperatingSystem", Required = Required.Always | Required.AllowNull)]
        public string OperatingSystem { get; set; }

        /// <summary>
        /// Identifies the node operating system for Linux systems from: <b>/etc/os-release</b>
        /// </summary>
        [JsonProperty(PropertyName = "OsImage", Required = Required.Always | Required.AllowNull)]
        public string OsImage { get; set; }

        /// <summary>
        /// Identifies the node's container runtime version.
        /// </summary>
        [JsonProperty(PropertyName = "ContainerRuntimeVersion", Required = Required.Always | Required.AllowNull)]
        public string ContainerRuntimeVersion { get; set; }

        /// <summary>
        /// Identifies the node's Kubelet version.
        /// </summary>
        [JsonProperty(PropertyName = "KubeletVersion", Required = Required.Always | Required.AllowNull)]
        public string KubeletVersion { get; set; }

        /// <summary>
        /// Identifies the node's private address.
        /// </summary>
        [JsonProperty(PropertyName = "PrivateAddress", Required = Required.Always | Required.AllowNull)]
        public string PrivateAddress { get; set; }
    }
}

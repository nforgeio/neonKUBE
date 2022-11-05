//-----------------------------------------------------------------------------
// FILE:	    ClusterInfo.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Runtime.Serialization;

using Newtonsoft.Json;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// Holds details about a cluster.
    /// </summary>
    public class ClusterInfo
    {
        /// <summary>
        /// Default constructor used for deserializion.
        /// </summary>
        public ClusterInfo()
        {
        }

        /// <summary>
        /// Used to construct an instance, picking up common properties from a
        /// cluster definition.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        public ClusterInfo(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));
            
            ClusterId          = clusterDefinition.Id;
            CreationTimestamp  = DateTime.UtcNow;
            ClusterVersion     = clusterDefinition.ClusterVersion;
            Name               = clusterDefinition.Name;
            Description        = clusterDefinition.Description;
            Environment        = clusterDefinition.Hosting.Environment;
            Purpose            = clusterDefinition.Purpose;
            Datacenter         = clusterDefinition.Datacenter;
            Latitude           = clusterDefinition.Latitude;
            Longitude          = clusterDefinition.Longitude;
            Domain             = clusterDefinition.Domain;
            PublicAddresses    = clusterDefinition.PublicAddresses;
            FeatureOptions     = clusterDefinition.Features;
        }

        /// <summary>
        /// Globally unique cluster identifier.  This is set during cluster setup and is 
        /// used to distinguish between customer clusters.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ClusterId { get; set; } = null;

        /// <summary>
        /// Timestamp representing the date that the cluster was created.
        /// </summary>
        [JsonProperty(PropertyName = "CreationTimestamp", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public DateTime? CreationTimestamp { get; set; }

        /// <summary>
        /// The neonKUBE version of the cluster.  This is formatted as a <see cref="SemanticVersion"/>.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(KubeVersions.NeonKube)]
        public string ClusterVersion { get; set; } = KubeVersions.NeonKube;

        /// <summary>
        /// Identifies the cluster by name as specified by <see cref="ClusterDefinition.Name"/> in the cluster definition.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Optionally describes the cluster for humans.
        /// </summary>
        [JsonProperty(PropertyName = "Description", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Description { get; set; } = null;

        /// <summary>
        /// Identifies the cloud or other hosting platform.
        /// definition. 
        /// </summary>
        [JsonProperty(PropertyName = "Environment", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(HostingEnvironment.Unknown)]
        public HostingEnvironment Environment { get; set; } = HostingEnvironment.Unknown;

        /// <summary>
        /// Indicates how the cluster is being used as specified by <see cref="ClusterDefinition.Purpose"/>.
        /// definition. 
        /// </summary>
        [JsonProperty(PropertyName = "Purpose", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(ClusterPurpose.Unspecified)]
        public ClusterPurpose Purpose { get; set; } = ClusterPurpose.Unspecified;

        /// <summary>
        /// Identifies where the cluster is hosted as specified by <see cref="ClusterDefinition.Datacenter"/> in the cluster
        /// definition.  That property defaults to the empty string for on-premise clusters and the the region for cloud
        /// based clusters.
        /// </summary>
        [JsonProperty(PropertyName = "Datacenter", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("")]
        public string Datacenter { get; set; } = string.Empty;

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
        /// Identifies the DNS domain assigned to the cluster when it was provisioned.
        /// </summary>
        [JsonProperty(PropertyName = "Domain", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("")]
        public string Domain { get; set; } = string.Empty;

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
        /// Human readable string that summarizes the cluster state.
        /// </summary>
        [JsonProperty(PropertyName = "Summary", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Summary { get; set; } = null;

        /// <summary>
        /// Describes which optional components have been deployed to the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "FeatureOptions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public FeatureOptions FeatureOptions { get; set; } = new FeatureOptions();
    }
}

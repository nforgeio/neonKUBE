//-----------------------------------------------------------------------------
// FILE:	    KubeSetupDetails.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;
using Neon.Kube.ClusterDef;

namespace Neon.Kube.Kube
{
    /// <summary>
    /// Holds details required during setup or for provisioning 
    /// additional cluster nodes.
    /// </summary>
    public class KubeSetupDetails
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeSetupDetails()
        {
        }

        /// <summary>
        /// The unique cluster ID.  This is generated during cluster setup and must not be specified by the user.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ClusterId { get; set; }

        /// <summary>
        /// <para>
        /// Specifies the cluster DNS domain.  neonKUBE generates a domain like <b>GUID.neoncluster.io</b>
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
        [JsonProperty(PropertyName = "ClusterDomain", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterDomain", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ClusterDomain { get; set; }

        /// <summary>
        /// Specifies the cluster definition.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterDefinition", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterDefinition", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ClusterDefinition ClusterDefinition { get; set; }

        /// <summary>
        /// Specifies the neonKUBE version of the cluster.  This is formatted as a <see cref="SemanticVersion"/>.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterVersion", ApplyNamingConventions = false)]
        [DefaultValue(KubeVersions.NeonKube)]
        public string ClusterVersion { get; set; } = KubeVersions.NeonKube;

        /// <summary>
        /// Specifies any public IP addresses for the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "PublicAddresses", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "publicAddresses", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> PublicAddresses { get; set; } = null;

        /// <summary>
        /// Indicates the cluster deployment state.
        /// </summary>
        [JsonProperty(PropertyName = "DeploymentStatus", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "deploymentStatus", ApplyNamingConventions = false)]
        [DefaultValue(ClusterDeploymentStatus.Unknown)]
        public ClusterDeploymentStatus DeploymentStatus { get; set; } = ClusterDeploymentStatus.Unknown;

        /// <summary>
        /// Specifies the command to be used join nodes to an existing cluster.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterJoinCommand", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterJoinCommand", ScalarStyle = ScalarStyle.Literal, ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ClusterJoinCommand { get; set; }

        /// <summary>
        /// Holds files captured from the boot control-plane node that will need to be provisioned
        /// on the remaining control-plane nodes.  The dictionary key is the file path and the value 
        /// specifies the file text, permissions, and owner.
        /// </summary>
        [JsonProperty(PropertyName = "ControlNodeFiles", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "controlNodeFiles", ScalarStyle = ScalarStyle.Literal, ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Dictionary<string, KubeFileDetails> ControlNodeFiles { get; set; } = new Dictionary<string, KubeFileDetails>();

        /// <summary>
        /// Holds the JWT used to authenticate with neonCLOUD headend services.
        /// </summary>
        [JsonProperty(PropertyName = "NeonCloudToken", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "neonCloudToken", ScalarStyle = ScalarStyle.Literal, ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string NeonCloudToken { get; set; }

        /// <summary>
        /// Specifies the public and private parts of the SSH client key used to authenticate a SSH session with a cluster node.
        /// </summary>
        [JsonProperty(PropertyName = "SshClientKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "sshClientKey", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public KubeSshKey SshKey { get; set; }
    }
}

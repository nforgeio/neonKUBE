//-----------------------------------------------------------------------------
// FILE:	    KubeClusterInfo.cs
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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Kube;
using Neon.Kube.ClusterDef;
using Neon.SSH;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Neon.Kube.Config
{
    /// <summary>
    /// Holds extended neonKUBE related cluster information.
    /// </summary>
    public class KubeClusterInfo
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeClusterInfo()
        {
        }

        /// <summary>
        /// Specifies a UUID for the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ClusterId { get; set; }

        /// <summary>
        /// Specifies the cluster name.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterName", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ClusterName { get; set; }

        /// <summary>
        /// Specifies the cluster domain.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterDomain", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterDomain", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ClusterDomain { get; set; } = null;

        /// <summary>
        /// Specifies the neonKUBE version of the cluster.  This is formatted as a <see cref="SemanticVersion"/>.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterVersion", ApplyNamingConventions = false)]
        [DefaultValue(KubeVersions.NeonKube)]
        public string ClusterVersion { get; set; } = KubeVersions.NeonKube;

        /// <summary>
        /// Specifies the SSO admin username.
        /// </summary>
        [JsonProperty(PropertyName = "SsoUsername", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "ssoUsername", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SsoUsername { get; set; }

        /// <summary>
        /// Specifies the SSO admin password.
        /// </summary>
        [JsonProperty(PropertyName = "SsoPassword", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "ssoPassword", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SsoPassword { get; set; }

        /// <summary>
        /// Specifies the SSH admin password for the cluster nodes.
        /// </summary>
        [JsonProperty(PropertyName = "SshUsername", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "sshUsername", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SshUsername { get; set; }

        /// <summary>
        /// <para>
        /// Specifies the SSH admin password for the cluster nodes.
        /// </para>
        /// <note>
        /// Technically, this is actually the admin user account password on the cluster nodes,
        /// not an SSH password because clusters disable SSH password authentication.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "SshPassword", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "sshPassword", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SshPassword { get; set; }

        /// <summary>
        /// Returns a <see cref="SshCredentials"/> instance suitable for connecting to a cluster node.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public SshCredentials SshCredentials
        {
            get
            {
                if (SshKey.PrivatePEM != null)
                {
                    return SshCredentials.FromPrivateKey(SshUsername, SshKey.PrivatePEM);
                }
                else if (SshUsername != null && SshPassword != null)
                {
                    return SshCredentials.FromUserPassword(SshUsername, SshPassword);
                }
                else
                {
                    return SshCredentials.None;
                }
            }
        }

        /// <summary>
        /// The public and private parts of the SSH client key used to
        /// authenticate a SSH session with a cluster node.
        /// </summary>
        [JsonProperty(PropertyName = "SshClientKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "sshClientKey", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public KubeSshKey SshKey { get; set; }
    }
}

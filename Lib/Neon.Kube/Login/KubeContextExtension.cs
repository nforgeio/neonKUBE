//-----------------------------------------------------------------------------
// FILE:	    KubeContextExtension.cs
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
using Neon.Cryptography;

namespace Neon.Kube
{
    /// <summary>
    /// Holds extended cluster information such as the cluster definition and
    /// node SSH credentials.  These records are persisted as files to the 
    /// <b>$HOME/.neonkube/clusters</b> folder in YAML files named like
    /// <b><i>USER</i>@<i>NAME</i>.context.yaml</b>.
    /// </summary>
    public class KubeContextExtension
    {
        private object syncRoot = new object();
        private string path;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeContextExtension()
        {
        }

        /// <summary>
        /// Parameterized constructor.
        /// </summary>
        /// <param name="path">Optionally specifies the path to the extension file.</param>
        public KubeContextExtension(string path)
        {
            this.path = path;
        }

        /// <summary>
        /// Set to a globally unique ID to identify the cluster.  This defaults to 
        /// a gewnerated unique value.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterId", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public Guid ClusterId { get; set; } = Guid.NewGuid();

        /// <summary>
        /// The cluster definition.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterDefinition", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterDefinition", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public ClusterDefinition ClusterDefinition { get; set; }

        /// <summary>
        /// Holds additional information required during setup as well as for
        /// provisoning additional clsuter nodes.
        /// </summary>
        [JsonProperty(PropertyName = "SetupDetails", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "setupDetails", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public KubeSetupDetails SetupDetails { get; set; } = new KubeSetupDetails();

        /// <summary>
        /// The SSH root username.
        /// </summary>
        [JsonProperty(PropertyName = "SshUsername", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "sshUsername", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SshUsername { get; set; }

        /// <summary>
        /// The SSH root password.
        /// </summary>
        [JsonProperty(PropertyName = "SshPassword", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "sshPassword", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SshPassword { get; set; }

        /// <summary>
        /// Returns a <see cref="SshCredentials"/> instance suitable for connecting to
        /// a cluster node.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public SshCredentials SshCredentials
        {
            get
            {
                if (SshUsername != null && SshPassword != null)
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
        /// The custom certificate generated for the Kubernetes dashboard PEM.
        /// </summary>
        [JsonProperty(PropertyName = "KubernetesDashboardCertificate")]
        [YamlMember(Alias = "kubernetesDashboardCertificate", ScalarStyle = ScalarStyle.Literal, ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string KubernetesDashboardCertificate { get; set; }

        /// <summary>
        /// The SSH RSA private key fingerprint used to secure the cluster nodes.  This is a
        /// MD5 hash encoded as hex bytes separated by colons.
        /// </summary>
        [JsonProperty(PropertyName = "SshNodeFingerprint")]
        [YamlMember(Alias = "sshNodeFingerprint", ScalarStyle = ScalarStyle.Literal, ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public string SshNodeFingerprint { get; set; }

        /// <summary>
        /// The SSH RSA private key used to secure the cluster nodes.
        /// </summary>
        [JsonProperty(PropertyName = "SshNodePrivateKey")]
        [YamlMember(Alias = "sshNodePrivateKey", ScalarStyle = ScalarStyle.Literal, ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public string SshNodePrivateKey { get; set; }

        /// <summary>
        /// The SSH RSA private key used to secure the cluster nodes.
        /// </summary>
        [JsonProperty(PropertyName = "SshNodePublicKey")]
        [YamlMember(Alias = "sshNodePublicKey", ScalarStyle = ScalarStyle.Literal, ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public string SshNodePublicKey { get; set; }

        /// <summary>
        /// The public and private parts of the SSH client key used to
        /// authenticate an SSH session with a cluster node.
        /// </summary>
        [JsonProperty(PropertyName = "SshClientKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "sshClientKey", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public SshClientKey SshClientKey { get; set; }

        /// <summary>
        /// Sets the file path where the extension will be persisted.
        /// </summary>
        /// <param name="path">The target path.</param>
        internal void SetPath(string path)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(path), nameof(path));

            this.path = path;
        }

        /// <summary>
        /// <para>
        /// Persists the extension data.
        /// </para>
        /// <note>
        /// A valid path must have been passed to the constructor for this to work.
        /// </note>
        /// </summary>
        public void Save()
        {
            lock (syncRoot)
            {
                if (ClusterId == Guid.Empty)
                {
                    ClusterId = Guid.NewGuid();
                }

                File.WriteAllText(path, NeonHelper.YamlSerialize(this));
            }
        }
    }
}

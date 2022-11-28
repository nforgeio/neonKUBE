//-----------------------------------------------------------------------------
// FILE:	    ClusterLogin.cs
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
using Neon.SSH;

namespace Neon.Kube
{
    /// <summary>
    /// Holds extended cluster information such as the cluster definition and
    /// node SSH credentials.  These records are persisted as files to the 
    /// <b>$HOME/.neonkube/logins</b> folder in YAML files named like
    /// <b><i>USER</i>@<i>NAME</i>.login.yaml</b>.
    /// </summary>
    public class ClusterLogin
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Reads a <see cref="ClusterLogin"/> from a file if it exists.
        /// </summary>
        /// <param name="path">Path the the cluster login file.</param>
        /// <returns>The <see cref="ClusterLogin"/> if the file exists or <c>null</c>.</returns>
        public static ClusterLogin Load(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var clusterLogin = NeonHelper.YamlDeserialize<ClusterLogin>(File.ReadAllText(path), strict: true);

            clusterLogin.path = path;

            return clusterLogin;
        }

        //---------------------------------------------------------------------
        // Instance members

        private object      syncRoot = new object();
        private string      path;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ClusterLogin()
        {
        }

        /// <summary>
        /// Parameterized constructor.
        /// </summary>
        /// <param name="path">Optionally specifies the path to the extension file.</param>
        public ClusterLogin(string path)
        {
            this.path = path;
        }

        /// <summary>
        /// Set to a globally unique ID to identify the cluster.  This defaults to 
        /// a generated unique value.
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
        /// provisoning additional cluster nodes.
        /// </summary>
        [JsonProperty(PropertyName = "SetupDetails", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "setupDetails", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public KubeSetupDetails SetupDetails { get; set; } = new KubeSetupDetails();

        /// <summary>
        /// The custom certificate generated for the Kubernetes dashboard PEM.
        /// </summary>
        [JsonProperty(PropertyName = "DashboardCertificate")]
        [YamlMember(Alias = "dashboardCertificate", ScalarStyle = ScalarStyle.Literal, ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string DashboardCertificate { get; set; }

        /// <summary>
        /// The root single sign-on (SSO) cluster username.
        /// </summary>
        [JsonProperty(PropertyName = "SsoUsername", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "ssoUsername", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SsoUsername { get; set; }

        /// <summary>
        /// The root single sign-on (SSO) cluster password.
        /// </summary>
        [JsonProperty(PropertyName = "SsoPassword", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "ssoPassword", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SsoPassword { get; set; }

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

        /// <summary>
        /// The neonKUBE version of the cluster.  This is formatted as a <see cref="SemanticVersion"/>.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterVersion", ApplyNamingConventions = false)]
        [DefaultValue(KubeVersions.NeonKube)]
        public string ClusterVersion { get; set; } = KubeVersions.NeonKube;

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

        /// <summary>
        /// Deletes the login.
        /// </summary>
        public void Delete()
        {
            NeonHelper.DeleteFile(path);
        }
    }
}

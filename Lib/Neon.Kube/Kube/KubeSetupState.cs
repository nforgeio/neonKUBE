//-----------------------------------------------------------------------------
// FILE:        KubeSetupState.cs
// CONTRIBUTOR: Jeff Lill
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

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;
using Neon.Kube.ClusterDef;
using Neon.Kube.Config;
using Neon.SSH;

namespace Neon.Kube
{
    /// <summary>
    /// Holds cluster provisioning related state.
    /// </summary>
    /// <remarks>
    /// <para>
    /// NEONKUBE cluster provisioning includes two major phases, **prepare** and **setup**,
    /// where preparing the cluster involves initializing infrastructure, including configuring
    /// the network and creating the virtual machines that will host the cluster.  Cluster
    /// setup is where we configure Kubernetes, install the necessary components, and then
    /// wrap up any network configuration.
    /// </para>
    /// <para>
    /// The prepare and setup steps need a temporary place to persist information that will
    /// bew required later.  For example, prepare generates the SSH credentials that setup
    /// will need to perform node operations.  This class persists its state to YAML files
    /// located in the <see cref="KubeHelper.SetupFolder"/>, with the file names set to the
    /// Kubernetes context name for the cluster.
    /// </para>
    /// </remarks>
    public class KubeSetupState
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Creates a new <see cref="KubeSetupState"/> instance to be used to persist
        /// setup state about the named cluster.
        /// </summary>
        /// <param name="contextName">Specifies the Kubernetes context name for the cluster.</param>
        /// <returns>The new instance.</returns>
        /// <remarks>
        /// <note>
        /// This removes any existing file persisting this information.
        /// </note>
        /// </remarks>
        public static KubeSetupState Create(string contextName)
        {
            Covenant.Requires<ArgumentNullException>(contextName != null, nameof(contextName));

            var path = GetPath(contextName);

            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return new KubeSetupState() 
            { 
                path        = path,
                ContextName = contextName
            };
        }

        /// <summary>
        /// Loads setup state for the named cluster from its file.
        /// </summary>
        /// <param name="contextName">Specifies the Kubernetes context name for the cluster.</param>
        /// <param name="nullIfMissing">Optionally return <c>null</c> instead of throwing an exception when the setup state file doesn't exist.</param>
        /// <returns>The instance loaded from the file.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file doesn't exist and <paramref name="nullIfMissing"/> is <c>false</c>.</exception>
        public static KubeSetupState Load(string contextName, bool nullIfMissing = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(contextName), nameof(contextName));

            var path = GetPath(contextName);

            if (!File.Exists(path))
            {
                if (nullIfMissing)
                {
                    return null;
                }
                else
                {
                    throw new FileNotFoundException($"File not found: {path}");
                }
            }

            var setupState = NeonHelper.JsonDeserialize<KubeSetupState>(File.ReadAllBytes(path));

            setupState.path = path;

            if (setupState.PublicAddresses == null)
            {
                setupState.PublicAddresses = new List<string>();
            }

            return setupState;
        }

        /// <summary>
        /// Loads setup state for the named cluster from its file when that exists, otherwise
        /// creates an unintialized instance.
        /// </summary>
        /// <param name="contextName">Specifies the Kubernetes context name for the cluster.</param>
        /// <returns>The instance loaded or created instance.</returns>
        /// <exception cref="FileNotFoundException">Thrown when the file doesn't exist.</exception>
        public static KubeSetupState LoadOrCreate(string contextName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(contextName), nameof(contextName));

            var path = GetPath(contextName);

            if (File.Exists(path))
            {
                var setupState = NeonHelper.JsonDeserialize<KubeSetupState>(File.ReadAllBytes(path));

                setupState.path = path;

                if (setupState.PublicAddresses == null)
                {
                    setupState.PublicAddresses = new List<string>();
                }

                return setupState;
            }
            else
            {
                return Create(contextName);
            }
        }

        /// <summary>
        /// Determines whether a setup state file exists for a cluster.
        /// </summary>
        /// <param name="contextName">Specifies the Kubernetes context name for the cluster.</param>
        /// <returns><c>true</c> when the file exists.</returns>
        public static bool Exists(string contextName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(contextName), nameof(contextName));

            return File.Exists(GetPath(contextName));
        }

        /// <summary>
        /// Deletes the setup state file for a cluster, if it exists.
        /// </summary>
        /// <param name="contextName">Specifies the Kubernetes context name for the cluster.</param>
        /// <returns><c>true</c> when the file exists.</returns>
        public static void Delete(string contextName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(contextName), nameof(contextName));

            var path = GetPath(contextName);

            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Returns the file path where the details will be persisted.
        /// </summary>
        /// <param name="contextName">Specifies the Kubernetes context name for the cluster.</param>
        /// <returns>The file path.</returns>
        public static string GetPath(string contextName)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(contextName), nameof(contextName));

            return Path.Combine(KubeHelper.SetupFolder, $"{contextName}.yaml");
        }

        //---------------------------------------------------------------------
        // Instance members

        private string  path;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public KubeSetupState()
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
        /// The cluster name.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterName", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ClusterName { get; set; }

        /// <summary>
        /// <para>
        /// Specifies the cluster DNS domain.  NEONKUBE generates a domain like <b>GUID.neoncluster.io</b>
        /// for your cluster by default when this is not set.
        /// </para>
        /// <note>
        /// Setting this to a specific domain that you've already registered is not supported at
        /// this time and will be ignored.
        /// </note>
        /// </summary>
        /// <remarks>
        /// <para>
        /// The idea here is that NEONKUBE will be use the generated domain to deploy a fully
        /// functional cluster out-of-the-box, with real DNS records and a SSL certificate.
        /// This works even for clusters deployed behind a firewall or NEONDESKTOP clusters
        /// running on a workstation or laptop.
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
        /// Specifies the NEONKUBE version of the cluster.  This is formatted as a <see cref="SemanticVersion"/>.
        /// </summary>
        [JsonProperty(PropertyName = "ClusterVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "clusterVersion", ApplyNamingConventions = false)]
        [DefaultValue(KubeVersion.NeonKube)]
        public string ClusterVersion { get; set; } = KubeVersion.NeonKube;

        /// <summary>
        /// Specifies any public IP addresses for the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "PublicAddresses", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "publicAddresses", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<string> PublicAddresses { get; set; } = new List<string>();

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
        /// Holds the JWT used to authenticate with NEONCLOUD headend services.
        /// </summary>
        [JsonProperty(PropertyName = "NeonCloudToken", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "neonCloudToken", ScalarStyle = ScalarStyle.Literal, ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string NeonCloudToken { get; set; }

        /// <summary>
        /// Identifies the KubeConfig context that will be created locally for the cluster being setup.
        /// </summary>
        [JsonProperty(PropertyName = "ContextName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "contextName", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string ContextName { get; set; }

        /// <summary>
        /// The SSO admin username.
        /// </summary>
        [JsonProperty(PropertyName = "SsoUsername", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "ssoUsername", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SsoUsername { get; set; }

        /// <summary>
        /// The SSO admin password.
        /// </summary>
        [JsonProperty(PropertyName = "SsoPassword", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "ssoPassword", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SsoPassword { get; set; }

        /// <summary>
        /// The SSH admin username.
        /// </summary>
        [JsonProperty(PropertyName = "SshUsername", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "sshUsername", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string SshUsername { get; set; }

        /// <summary>
        /// <para>
        /// Specifies the SSH admin password.
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
        /// Specifies the public and private parts of the SSH client key used to authenticate a SSH session with a cluster node.
        /// </summary>
        [JsonProperty(PropertyName = "SshClientKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "sshClientKey", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public KubeSshKey SshKey { get; set; }

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
        /// Persists the details to its associated file.
        /// </summary>
        public void Save()
        {
            File.WriteAllText(path, NeonHelper.JsonSerialize(this, Formatting.Indented));
        }

        /// <summary>
        /// Removes the file associated with the details, if it exists.
        /// </summary>
        public void Delete()
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Returns a <see cref="KubeClusterInfo"/> instance initialized from this instance.
        /// </summary>
        /// <returns>The <see cref="KubeClusterInfo"/>.</returns>
        public KubeClusterInfo ToKubeClusterInfo()
        {
            return new KubeClusterInfo()
            {
                ClusterId      = this.ClusterId,
                ClusterName    = this.ClusterName,
                ClusterDomain  = this.ClusterDomain,
                ClusterVersion = this.ClusterVersion,
                SshUsername    = this.SshUsername,
                SshPassword    = this.SshPassword,
                SshKey         = this.SshKey,
                SsoUsername    = this.SsoUsername,
                SsoPassword    = this.SsoPassword
            };
        }
    }
}

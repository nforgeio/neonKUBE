//-----------------------------------------------------------------------------
// FILE:	    ClusterLogin.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

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

using Neon.Common;
using Neon.Cryptography;

namespace Neon.Cluster
{
    /// <summary>
    /// <para>
    /// Holds the <b>sensitive</b> information required to remotely manage an operating
    /// neonCLUSTER using the <b>neon-cli</b>.
    /// </para>
    /// <note>
    /// <b>WARNING:</b> The information serialized by this class must be carefully protected
    /// because it can be used to assume control over a cluster.
    /// </note>
    /// </summary>
    public class ClusterLogin
    {
        /// <summary>
        /// Returns the cluster name.
        /// </summary>
        [JsonIgnore]
        public string ClusterName
        {
            get { return Definition?.Name; }
        }

        /// <summary>
        /// Returns the login name formatted as: USERNAME@CLUSTERNAME
        /// </summary>
        [JsonIgnore]
        public string LoginName
        {
            get
            {
                if (Definition != null && Username != null)
                {
                    return $"{Username}@{ClusterName}";
                }
                else
                {
                    return string.Empty;
                }
            }
        }

        /// <summary>
        /// The optional file system path where the cluster login is persisted.  This is
        /// used by the <see cref="Save"/> method.
        /// </summary>
        [JsonIgnore]
        public string Path { get; set; }

        /// <summary>
        /// The operator's username associated with these cluster secrets.
        /// </summary>
        [JsonProperty(PropertyName = "Username", Required = Required.Always)]
        public string Username { get; set; }

        /// <summary>
        /// Specifies whether communication with the cluster should be made via
        /// the VPN or directly (the default).
        /// </summary>
        [JsonIgnore]
        public bool ViaVpn { get; set; }

        /// <summary>
        /// The cluster definition.
        /// </summary>
        [JsonProperty(PropertyName = "Definition", Required = Required.Always)]
        public ClusterDefinition Definition { get; set; }

        /// <summary>
        /// Indicates that the credentials are not fully initialized.  This will be <c>true</c> when
        /// a cluster has been prepared but has not yet been fully setup.
        /// </summary>
        [JsonProperty(PropertyName = "PartialSetup", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool PartialSetup { get; set; }

        /// <summary>
        /// Indicates that the login has cluster root capabilities (e.g. managing the cloud infrastructure and other user logins).
        /// </summary>
        [JsonProperty(PropertyName = "IsRoot", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool IsRoot { get; set; }

        /// <summary>
        /// The root SSH username for the cluster nodes.
        /// </summary>
        [JsonProperty(PropertyName = "SshUsername", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string SshUsername { get; set; }

        /// <summary>
        /// The root SSH password password for the cluster nodes.
        /// </summary>
        [JsonProperty(PropertyName = "SshPassword", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string SshPassword { get; set; }

        /// <summary>
        /// Indicates whether a strong host SSH password was generated for the cluster.
        /// This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "HasStrongSshPassword", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool HasStrongSshPassword { get; set; }

        /// <summary>
        /// The public and private parts of the SSH client key when the cluster is
        /// configured to authenticate clients via public keys or <c>null</c> when
        /// username/password authentication is enabled.
        /// </summary>
        [JsonProperty(PropertyName = "SshClientKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public SshClientKey SshClientKey { get; set; }

        /// <summary>
        /// The SSH RSA private key fingerprint used to secure the cluster servers.  This is an 
        /// MD5 hash encoded as hex bytes separated by colons.
        /// </summary>
        [JsonProperty(PropertyName = "SshServerKeyFingerprint")]
        public string SshServerKeyFingerprint { get; set; }

        /// <summary>
        /// The SSH RSA private key used to secure the cluster servers.
        /// </summary>
        [JsonProperty(PropertyName = "SshServerKey")]
        public string SshServerKey { get; set; }

        /// <summary>
        /// The HashiCorp Vault credentials.
        /// </summary>
        [JsonProperty(PropertyName = "VaultCredentials", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public VaultCredentials VaultCredentials { get; set;}

        /// <summary>
        /// The HashiCorp Vault self-signed certificate.
        /// </summary>
        [JsonProperty(PropertyName = "VaultCertificate", Required = Required.Default)]
        [DefaultValue(null)]
        public TlsCertificate VaultCertificate { get; set; }

        /// <summary>
        /// The Docker manager node swarm join key.
        /// </summary>
        [JsonProperty(PropertyName = "SwarmManagerToken", Required = Required.Default)]
        [DefaultValue(null)]
        public string SwarmManagerToken { get; set; }

        /// <summary>
        /// The Docker worker node swarm join key.
        /// </summary>
        [JsonProperty(PropertyName = "SwarmWorkerToken", Required = Required.Default)]
        [DefaultValue(null)]
        public string SwarmWorkerToken { get; set; }

        /// <summary>
        /// The VPN credentials if a management VPN is enabled for the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "VpnCredentials", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public VpnCredentials VpnCredentials { get; set; }

        /// <summary>
        /// Dictionary of Docker registry TLS certifcates indexed by host manager node name.
        /// </summary>
        [JsonProperty(PropertyName = "RegistryCerts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, string> RegistryCerts { get; set; }

        /// <summary>
        /// Dictionary of Docker registry TLS private keys indexed by host manager name.
        /// </summary>
        [JsonProperty(PropertyName = "RegistryKeys", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, string> RegistryKeys { get; set; }

        /// <summary>
        /// Returns the <see cref="SshCredentials"/> for the cluster that can be used
        /// by <see cref="NodeProxy{TMetadata}"/> and the <b>SSH.NET</b> Nuget package.
        /// </summary>
        /// <returns></returns>
        public SshCredentials GetSshCredentials()
        {
            if (SshClientKey != null)
            {
                return SshCredentials.FromPrivateKey(SshUsername, SshClientKey.PrivatePEM);
            }
            else
            {
                return SshCredentials.FromUserPassword(SshUsername, SshPassword);
            }
        }

        /// <summary>
        /// Clears all root user secrets.
        /// </summary>
        public void ClearRootSecrets()
        {
            IsRoot            = false;
            SwarmManagerToken = null;
            SwarmWorkerToken  = null;
            SshServerKey      = null;
            RegistryCerts     = null;
            RegistryKeys      = null;

            if (VpnCredentials != null)
            {
                VpnCredentials.CaZipKey = null;
            }

            // Clear the provider specific information because it
            // contains hosting credentials.

            Definition.Hosting.ClearSecrets();
        }

        /// <summary>
        /// Returns a deep clone of the instance
        /// </summary>
        /// <returns>The clone.</returns>
        public ClusterLogin Clone()
        {
            var json = NeonHelper.JsonSerialize(this);

            return NeonHelper.JsonDeserialize<ClusterLogin>(json);
        }

        /// <summary>
        /// Persists the cluster login information as JSON to the file system at <see cref="Path"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown is <see cref="Path"/> is not set.</exception>
        public void Save()
        {
            if (string.IsNullOrEmpty(Path))
            {
                throw new InvalidOperationException($"[{nameof(ClusterLogin)}]  cannot be saved because [{nameof(Path)}] is null.");
            }

            File.WriteAllText(Path, NeonHelper.JsonSerialize(this, Formatting.Indented));
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return LoginName;
        }
    }
}

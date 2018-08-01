//-----------------------------------------------------------------------------
// FILE:	    DebugSecrets.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Consul;
using Newtonsoft.Json;

using Neon.Common;

namespace Neon.Hive
{
    /// <summary>
    /// Used to emulate Docker service secrets when debugging an application using 
    /// <see cref="HiveHelper.OpenHiveRemote(DebugSecrets, DebugConfigs, string, bool)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Add simple text secrets to the collection using <see cref="Add(string, string)"/>.
    /// </para>
    /// <para>
    /// You can also create temporary hive Vault and Consul credentials using
    /// <see cref="VaultAppRole(string, string)"/> and <see cref="ConsulToken(string, string[])"/>.
    /// Temporary credentials have a lifespan of 1 day by default, but this can be
    /// changed by setting <see cref="CredentialTTL"/>.
    /// </para>
    /// </remarks>
    public class DebugSecrets : Dictionary<string, string>
    {
        //---------------------------------------------------------------------
        // Private types

        private enum CredentialType
        {
            VaultToken,
            VaultAppRole,
            ConsulToken
        }

        private class CredentialRequest
        {
            public CredentialType   Type;
            public string           SecretName;
            public string           RoleName;
            public string           Token;
        }

        //---------------------------------------------------------------------
        // Implementation

        private List<CredentialRequest> credentialRequests = new List<CredentialRequest>();
        private HiveLogin               hiveLogin;
        private VaultClient             vaultClient;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public DebugSecrets()
            : base(StringComparer.InvariantCultureIgnoreCase)
        {
        }

        /// <summary>
        /// The lifespan of Vault and Consul credentials created by this class.  This defaults
        /// to 1 day, but may be modified by applications.
        /// </summary>
        public TimeSpan CredentialTTL { get; set; } = TimeSpan.FromDays(1);

        /// <summary>
        /// Adds a string secret.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <param name="value">The secret value string.</param>
        /// <returns>The current instance to support fluent-style coding.</returns>
        public new DebugSecrets Add(string name, string value)
        {
            value = value ?? string.Empty;

            base.Add(name, value);

            return this;
        }

        /// <summary>
        /// Adds a secret object as JSON.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <param name="value">The secret value.</param>
        /// <returns>The current instance to support fluent-style coding.</returns>
        public DebugSecrets Add(string name, object value)
        {
            value = value ?? string.Empty;

            base.Add(name, NeonHelper.JsonSerialize(value, Formatting.Indented));

            return this;
        }

        /// <summary>
        /// Adds Vault token credentials to the dictionary.  The credentials will be
        /// formatted as <see cref="HiveCredentials"/> serialized to JSON.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <param name="token">The Vault token.</param>
        /// <returns>The current instance to support fluent-style coding.</returns>
        public DebugSecrets VaultToken(string name, string token)
        {
            credentialRequests.Add(
                new CredentialRequest()
                {
                    Type       = CredentialType.VaultToken,
                    SecretName = name,
                    Token      = token
                });

            return this;
        }

        /// <summary>
        /// Adds Vault AppRole credentials to the dictionary.  The credentials will be
        /// formatted as <see cref="HiveCredentials"/> serialized to JSON.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <param name="roleName">The Vault role name.</param>
        /// <returns>The current instance to support fluent-style coding.</returns>
        public DebugSecrets VaultAppRole(string name, string roleName)
        {
            credentialRequests.Add(
                new CredentialRequest()
                {
                    Type       = CredentialType.VaultAppRole,
                    SecretName = name,
                    RoleName   = roleName
                });

            return this;
        }

        /// <summary>
        /// Creates a temporary Consul token with the specified access control policies
        /// and then adds the token as a named secret.
        /// </summary>
        /// <param name="name">The secret name.</param>
        /// <param name="policies">The Consul policy names or HCL.</param>
        /// <returns>The current instance to support fluent-style coding.</returns>
        public DebugSecrets ConsulToken(string name, params string[] policies)
        {
            // $todo(jeff.lill): Implement this.

            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns a <see cref="VaultClient"/> for the attached hive using the root token.
        /// </summary>
        private VaultClient VaultClient
        {
            get
            {
                if (vaultClient == null)
                {
                    vaultClient = HiveHelper.OpenVault(HiveCredentials.FromVaultToken(hiveLogin.VaultCredentials.RootToken));
                }

                return vaultClient;
            }
        }

        /// <summary>
        /// Called internally by <see cref="HiveHelper.OpenHiveRemote(DebugSecrets, DebugConfigs, string, bool)"/> 
        /// to create any requested Vault and Consul credentials and add them to the dictionary.
        /// </summary>
        /// <param name="hive">The attached hive.</param>
        /// <param name="hiveLogin">The hive login.</param>
        internal void Realize(HiveProxy hive, HiveLogin hiveLogin)
        {
            this.hiveLogin = hiveLogin;

            HiveCredentials credentials;

            foreach (var request in credentialRequests)
            {
                switch (request.Type)
                {
                    case CredentialType.VaultToken:

                        // Serialize the credentials as JSON and persist.

                        credentials = HiveCredentials.FromVaultToken(request.Token);

                        Add(request.SecretName, NeonHelper.JsonSerialize(credentials, Formatting.Indented));
                        break;

                    case CredentialType.VaultAppRole:

                        // Serialize the credentials as JSON and persist.

                        credentials = VaultClient.GetAppRoleCredentialsAsync(request.RoleName).Result;

                        Add(request.SecretName, NeonHelper.JsonSerialize(credentials, Formatting.Indented));
                        break;

                    case CredentialType.ConsulToken:

                        // $todo(jeff.lill): Implement this.

                        break;
                }
            }
        }
    }
}

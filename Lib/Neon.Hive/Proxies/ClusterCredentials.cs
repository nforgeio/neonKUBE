//-----------------------------------------------------------------------------
// FILE:	    ClusterCredentials.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.Hive
{
    /// <summary>
    /// Holds neonHIVE credentials.
    /// </summary>
    public class ClusterCredentials
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Constructs Vault token based credentials.
        /// </summary>
        /// <param name="token">The Vault token.</param>
        /// <returns>The <see cref="ClusterCredentials"/>.</returns>
        public static ClusterCredentials FromVaultToken(string token)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(token));

            return new ClusterCredentials()
            {
                Type       = ClusterCredentialsType.VaultToken,
                VaultToken = token
            };
        }

        /// <summary>
        /// Constructs Vault role based credentials.
        /// </summary>
        /// <param name="roleId">The Vault role ID.</param>
        /// <param name="secretId">The Vault role secret ID.</param>
        /// <returns>The <see cref="ClusterCredentials"/>.</returns>
        public static ClusterCredentials FromVaultRole(string roleId, string secretId)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(roleId));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(secretId));

            return new ClusterCredentials()
            {
                Type          = ClusterCredentialsType.VaultAppRole,
                VaultRoleId   = roleId,
                VaultSecretId = secretId
            };
        }

        /// <summary>
        /// Parses <see cref="ClusterCredentials"/> from a JSON string.
        /// </summary>
        /// <param name="json">The input.</param>
        /// <returns>The parsed <see cref="ClusterCredentials"/>.</returns>
        /// <exception cref="FormatException">Thrown if the credentials are not valid.</exception>
        public static ClusterCredentials ParseJson(string json)
        {
            var credentials = NeonHelper.JsonDeserialize<ClusterCredentials>(json);

            credentials.Validate();

            return credentials;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Constructor.
        /// </summary>
        public ClusterCredentials()
        {
        }

        /// <summary>
        /// Indicates the credentials type.
        /// </summary>
        [JsonProperty(PropertyName = "Type", Required = Required.Always)]
        public ClusterCredentialsType Type { get; set; }

        /// <summary>
        /// The Vault token for <see cref="ClusterCredentialsType.VaultToken"/> credentials.
        /// </summary>
        [JsonProperty(PropertyName = "VaultToken", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VaultToken { get; set; }

        /// <summary>
        /// The Vault role ID for <see cref="ClusterCredentialsType.VaultAppRole"/> credentials.
        /// </summary>
        [JsonProperty(PropertyName = "VaultRoleId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VaultRoleId { get; set; }

        /// <summary>
        /// The Vault secret ID for <see cref="ClusterCredentialsType.VaultAppRole"/> credentials.
        /// </summary>
        [JsonProperty(PropertyName = "VaultSecretId", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string VaultSecretId { get; set; }

        /// <summary>
        /// Validates the credentials.
        /// </summary>
        /// <exception cref="FormatException">Thrown if the credentials are not valid.</exception>
        public void Validate()
        {
            switch (Type)
            {
                case ClusterCredentialsType.VaultToken:

                    if (string.IsNullOrEmpty(VaultToken))
                    {
                        throw new FormatException($"[{nameof(VaultToken)}] is NULL or empty for [{Type}] cluster credential");
                    }
                    break;

                case ClusterCredentialsType.VaultAppRole:

                    if (string.IsNullOrEmpty(VaultRoleId))
                    {
                        throw new FormatException($"[{nameof(VaultRoleId)}] is NULL or empty for [{Type}] cluster credential");
                    }

                    if (string.IsNullOrEmpty(VaultSecretId))
                    {
                        throw new FormatException($"[{nameof(VaultSecretId)}] is NULL or empty for [{Type}] cluster credential");
                    }
                    break;

                default:

                    throw new FormatException($"Unexpected or supported cluster credentials type [{Type}].");
            }
        }
    }
}

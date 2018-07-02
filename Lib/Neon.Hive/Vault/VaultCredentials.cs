//-----------------------------------------------------------------------------
// FILE:	    VaultCredentials.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// Holds the keys necessary to unseal a HashiCorp Vault as well
    /// as the Vault's root token.
    /// </summary>
    public class VaultCredentials
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Parse Vault credentials from the results from a <b>vault init</b> command.
        /// </summary>
        /// <param name="rawCredentials">The output from the command.</param>
        /// <param name="keyThreshold">The minimum number of keys required to unseal the Vault.</param>
        /// <returns>The parsed <see cref="VaultCredentials"/>.</returns>
        public static VaultCredentials FromInit(string rawCredentials, int keyThreshold)
        {
            Covenant.Requires<ArgumentNullException>(rawCredentials != null);
            Covenant.Requires<ArgumentException>(keyThreshold > 0);

            const string TokenError = "Invalid HashiCorp Vault Root Token";
            const string KeyError   = "Invalid HashiCorp Vault Uunseal Key";

            var credentials = new VaultCredentials();

            // Extract the root token.

            var tokenPrefix = "Initial Root Token:";
            var pos         = rawCredentials.IndexOf(tokenPrefix);

            if (pos == -1)
            {
                throw new FormatException("Unable to locate the [Initial Root Token: ###] in the Hashcorp Vault credentials.");
            }

            pos += tokenPrefix.Length;

            var posEnd = rawCredentials.IndexOf('\n', pos);

            if (posEnd == -1)
            {
                throw new FormatException(TokenError);
            }

            credentials.RootToken = rawCredentials.Substring(pos, posEnd - pos).Trim();

            if (string.IsNullOrEmpty(credentials.RootToken))
            {
                throw new FormatException(TokenError);
            }

            // Parse the unseal keys.

            var keys = new List<string>();

            for (int i = 0; i < 10; i++)
            {
                var keyPrefix = $"Unseal Key {i + 1}:";

                pos = rawCredentials.IndexOf(keyPrefix);

                if (pos == -1)
                {
                    break;
                }

                pos   += keyPrefix.Length;
                posEnd = rawCredentials.IndexOf('\n', pos);

                if (posEnd == -1)
                {
                    throw new FormatException(KeyError);
                }

                var key = rawCredentials.Substring(pos, posEnd - pos).Trim();

                if (string.IsNullOrEmpty(key))
                {
                    throw new FormatException(TokenError);
                }

                keys.Add(key);
            }

            if (keys.Count == 0)
            {
                throw new FormatException("No HashiCorp Vault unseal keys were parsed.");
            }

            if (keys.Count < keyThreshold)
            {
                throw new FormatException($"HashiCorp Vault unseal key count [{keys.Count}] is less than the key threshold [{keyThreshold}].");
            }

            credentials.UnsealKeys   = keys;
            credentials.KeyThreshold = keyThreshold;

            return credentials;
        }

        /// <summary>
        /// Parse Vault credentials from JSON.
        /// </summary>
        /// <param name="json">The JSON text.</param>
        /// <returns>The parsed <see cref="VaultCredentials"/>.</returns>
        public static VaultCredentials FromJson(string json)
        {
            Covenant.Requires<ArgumentNullException>(json != null);

            var credentials = NeonHelper.JsonDeserialize<VaultCredentials>(json);

            if (credentials.UnsealKeys.Count == 0)
            {
                throw new FormatException("No HashiCorp Vault unseal keys were parsed.");
            }

            if (credentials.UnsealKeys.Count < credentials.KeyThreshold)
            {
                throw new FormatException($"HashiCorp Vault unseal key count [{credentials.UnsealKeys.Count}] is less than the key threshold [{credentials.KeyThreshold}].");
            }

            return credentials;
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public VaultCredentials()
        {
        }

        /// <summary>
        /// The root Vault token required to access the vault after it's been unsealed.
        /// </summary>
        [JsonProperty(PropertyName = "RootToken", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string RootToken { get; set; }

        /// <summary>
        /// The keys required to unseal the Vault.
        /// </summary>
        [JsonProperty(PropertyName = "UnsealKeys", Required = Required.Always | Required.DisallowNull)]
        public List<string> UnsealKeys { get; set; } = new List<string>();

        /// <summary>
        /// The number of keys that are required to unseal the Vault.
        /// </summary>
        [JsonProperty(PropertyName = "KeyThreshold", Required = Required.Always)]
        public int KeyThreshold { get; set; }
    }
}

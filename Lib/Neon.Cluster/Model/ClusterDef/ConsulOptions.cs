//-----------------------------------------------------------------------------
// FILE:	    ConsulOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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

namespace Neon.Cluster
{
    /// <summary>
    /// Describes the HashiCorp Consul options for a neonCLUSTER.
    /// </summary>
    public class ConsulOptions
    {
        private const string        defaultVersion = "0.9.3";
        private readonly Version    minVersion     = new System.Version("0.7.4");

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ConsulOptions()
        {
        }

        /// <summary>
        /// The version of Consul to be installed.  This defaults to a reasonable
        /// recent version.
        /// </summary>
        [JsonProperty(PropertyName = "Version", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultVersion)]
        public string Version { get; set; } = defaultVersion;

        /// <summary>
        /// The shared key used by Consul to encrypt network traffic between cluster nodes.
        /// This key must be 16-bytes, Base64 encoded.  This defaults to a cryptographically
        /// generated key.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Suitable keys may be generated via <b>neon create key</b>.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "EncryptionKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string EncryptionKey { get; set; } = null;

        /// <summary>
        /// Returns the Consul port.
        /// </summary>
        [JsonIgnore]
        public int Port
        {
            get { return NetworkPorts.Consul; }
        }

        /// <summary>
        /// Validates the options definition and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            if (!System.Version.TryParse(Version, out var version))
            {
                throw new ClusterDefinitionException($"Invalid version [{nameof(Version)}={Version}].");
            }

            if (version < minVersion)
            {
                throw new ClusterDefinitionException($"Minumim acceptable [{nameof(Version)}={minVersion}].");
            }

            if (string.IsNullOrEmpty(EncryptionKey))
            {
                EncryptionKey = Convert.ToBase64String(NeonHelper.RandBytes(16));
            }

            ClusterDefinition.VerifyEncryptionKey(EncryptionKey);
        }
    }
}

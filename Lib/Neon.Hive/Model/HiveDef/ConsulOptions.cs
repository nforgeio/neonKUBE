//-----------------------------------------------------------------------------
// FILE:	    ConsulOptions.cs
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
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// Describes the HashiCorp Consul options for a neonHIVE.
    /// </summary>
    public class ConsulOptions
    {
        private const string        defaultVersion = "1.1.0";
        private readonly Version    minVersion     = new System.Version("1.0.7");

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
        /// The shared key used by Consul to encrypt network traffic between hive nodes.
        /// This key must be 16-bytes, Base64 encoded.  This defaults to a generated 
        /// cryptographically random key.
        /// </summary>
        /// <remarks>
        /// <note>
        /// Suitable keys may be generated via <b>neon create cypher</b>.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "EncryptionKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string EncryptionKey { get; set; } = null;

        /// <summary>
        /// Returns the Consul port.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public int Port
        {
            get { return NetworkPorts.Consul; }
        }

        /// <summary>
        /// The time-to-live (TTL) in seconds returned for Consul DNS query responses.
        /// This defaults to <b>5 seconds</b> for better scalability.
        /// </summary>
        [JsonProperty(PropertyName = "DnsTTL", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(5)]
        public int DnsTTL { get; set; } = 5;

        /// <summary>
        /// Controls whether potentially stale DNS responses can be served by non-leader 
        /// Consul nodes.  Specify <b>0</b> if only the leader is allowed to generate
        /// DNS responses, otherwise specify the maximum number of seconds non-leaders
        /// will return stale responses.  This defaults to an essentially infinite
        /// value (about 10 years).
        /// </summary>
        /// <remarks>
        /// The default value allows for stale DNS responses to be returned indefinitely
        /// when Consul loses its quorum or a leader is not present.  This can help 
        /// allow a hive to continue to function during a partial Consul outage.
        /// </remarks>
        [JsonProperty(PropertyName = "DnsMaxStale", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(315360000)]
        public int DnsMaxStale { get; set; } = 315360000;

        /// <summary>
        /// Controls whether Consul is to be secured by HTTPS and a TLS certificate.
        /// This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Tls", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool Tls { get; set; } = true;

        /// <summary>
        /// Returns the HTTP/HTTPS scheme to be used to access the Consul REST API.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public string Scheme => Tls ? "https" : "http";

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="HiveDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(HiveDefinition hiveDefinition)
        {
            Covenant.Requires<ArgumentNullException>(hiveDefinition != null);

            if (!System.Version.TryParse(Version, out var version))
            {
                throw new HiveDefinitionException($"Invalid version [{nameof(ConsulOptions)}.{nameof(Version)}={Version}].");
            }

            if (version < minVersion)
            {
                throw new HiveDefinitionException($"Minumim acceptable [{nameof(ConsulOptions)}.{nameof(Version)}={minVersion}].");
            }

            if (string.IsNullOrEmpty(EncryptionKey))
            {
                EncryptionKey = Convert.ToBase64String(NeonHelper.RandBytes(16));
            }

            if (DnsTTL < 0)
            {
                throw new HiveDefinitionException($"[{nameof(ConsulOptions)}.{nameof(DnsTTL)}={DnsTTL}] is not valid.");
            }

            if (DnsMaxStale < 0)
            {
                throw new HiveDefinitionException($"[{nameof(ConsulOptions)}.{nameof(DnsMaxStale)}={DnsMaxStale}] is not valid.");
            }

            HiveDefinition.VerifyEncryptionKey(EncryptionKey);
        }
    }
}

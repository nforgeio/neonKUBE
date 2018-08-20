//-----------------------------------------------------------------------------
// FILE:	    KongOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

using Neon.Common;
using Neon.IO;

namespace Neon.Hive
{
    /// <summary>
    /// Specifies the options for configuring the hive <a href="https://konghq.com/">Kong API Gateway</a>.
    /// </summary>
    public class KongOptions
    {
        private const string defaultVersion = "0.14.0";

        /// <summary>
        /// Returns the supported Kong versions.  These map to the <b>nhive/kong</b> tags.
        /// </summary>
        private IEnumerable<string> SupportedVersions =
            new string[]
            {
                "0.14.0"
            };

        /// <summary>
        /// Indicates whether Kong API gateway is to be enabled for the hive.  
        /// This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Enabled", Required = Required.Default)]
        [DefaultValue(true)]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Specifies the Kong software version. This defaults to a reasonable recent release.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The following Kong versions are supported:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>0.14.0</b></term>
        ///     <description>
        ///     Released 07-2018 (<b>default</b>)
        ///     </description>
        /// </item>
        /// </list>
        /// </remarks>
        [JsonProperty(PropertyName = "Version", Required = Required.Default)]
        [DefaultValue(defaultVersion)]
        public string Version { get; set; } = defaultVersion;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="hiveDefinition">The hive definition.</param>
        /// <exception cref="HiveDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(HiveDefinition hiveDefinition)
        {
            if (!Enabled)
            {
                return;
            }

            Version = Version ?? defaultVersion;

            if (!SupportedVersions.Contains(Version))
            {
                throw new HiveDefinitionException($"[{Version}] is not a supported Kong release.");
            }

            // Validate the properties.

            if (string.IsNullOrWhiteSpace(Version))
            {
                Version = defaultVersion;
            }

            if (Version == string.Empty)
            {
                throw new HiveDefinitionException($"[{nameof(CephOptions)}.{nameof(Version)}={Version}] is not a valid.  Please specify something like [{defaultVersion}].");
            }
        }
    }
}

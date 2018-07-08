//-----------------------------------------------------------------------------
// FILE:	    RegistryCredentials.cs
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
using Neon.Cryptography;

namespace Neon.Hive
{
    /// <summary>
    /// Holds credentials for a Docker registry.
    /// </summary>
    public class RegistryCredentials
    {
        /// <summary>
        /// Identifies the target registry using its hostname.
        /// </summary>
        [JsonProperty(PropertyName = "Registry", Required = Required.Always)]
        [DefaultValue(null)]
        public string Registry { get; set; }

        /// <summary>
        /// The registry username.
        /// </summary>
        [JsonProperty(PropertyName = "Username", Required = Required.Always)]
        [DefaultValue(null)]
        public string Username { get; set; }

        /// <summary>
        /// The registry password.
        /// </summary>
        [JsonProperty(PropertyName = "Password", Required = Required.Always)]
        [DefaultValue(null)]
        public string Password { get; set; }
    }
}

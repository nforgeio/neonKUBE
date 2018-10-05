//-----------------------------------------------------------------------------
// FILE:	    ProxyUpdateMessage.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.Hive
{
    /// <summary>
    /// Published to the <see cref="HiveMQChannels.ProxyNotify"/> channel to notify
    /// <b>neon-proxy-public</b>, <b>neon-proxy-private</b>, <b>neon-proxy-public-bridge</b>,
    /// <b>neon-proxy-private-bridge</b>, <b>neon-proxy-public-cache</b>, and
    /// <b>neon--proxy-private-cache</b> service instances that their configuration
    /// has changed and should be reloaded.
    /// </summary>
    public class ProxyUpdateMessage
    {
        /// <summary>
        /// Indicates that <b>neon-proxy-public</b> should reload its configuration.
        /// </summary>
        [JsonProperty(PropertyName = "Public", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Public { get; set; } = false;

        /// <summary>
        /// Indicates that <b>neon-proxy-private</b> should reload its configuration.
        /// </summary>
        [JsonProperty(PropertyName = "Private", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool Private { get; set; } = false;

        /// <summary>
        /// Indicates that <b>neon-proxy-public-bridge</b> should reload its configuration.
        /// </summary>
        [JsonProperty(PropertyName = "PublicBridge", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool PublicBridge { get; set; } = false;

        /// <summary>
        /// Indicates that <b>neon-proxy-private-bridge</b> should reload its configuration.
        /// </summary>
        [JsonProperty(PropertyName = "PrivateBridge", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool PrivateBridge { get; set; } = false;

        /// <summary>
        /// Indicates that <b>neon-proxy-public-cache</b> should reload its configuration.
        /// </summary>
        [JsonProperty(PropertyName = "PublicCache", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool PublicCache { get; set; } = false;

        /// <summary>
        /// Indicates that <b>neon-proxy-private-cache</b> should reload its configuration.
        /// </summary>
        [JsonProperty(PropertyName = "PrivateCache", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool PrivateCache { get; set; } = false;
    }
}

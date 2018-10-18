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
    /// Broadcast to the <see cref="HiveMQChannels.ProxyNotify"/> channel to notify
    /// <b>neon-proxy-public</b>, <b>neon-proxy-private</b>, <b>neon-proxy-public-bridge</b>, 
    /// <b>neon-proxy-private-bridge</b>, <b>neon-proxy-public-cache</b>, and 
    /// <b>neon--proxy-private-cache</b> service instances that their configuration 
    /// has changed and should be reloaded.
    /// </summary>
    public class ProxyUpdateMessage
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ProxyUpdateMessage()
        {
        }

        /// <summary>
        /// Constructor that optionally initializes all boolean properties.
        /// </summary>
        /// <param name="all">Optionally indicates that all of the boolean properties should be initialized to <c>true</c>.</param>
        public ProxyUpdateMessage(bool all)
        {
            if (all)
            {
                PublicProxy   = true;
                PrivateProxy  = true;
                PublicBridge  = true;
                PrivateBridge = true;
                PublicCache   = true;
                PrivateCache  = true;
            }
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="reason">The human readable reason for the message.</param>
        public ProxyUpdateMessage(string reason)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(reason));

            this.Reason = reason;
        }

        /// <summary>
        /// Optionally describes why the message was sent as human readable text.  This
        /// defaults to <b>"Unknown"</b>.
        /// </summary>
        [JsonProperty(PropertyName = "Reason", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("Unknown")]
        public string Reason { get; set; } = "Unknown";

        /// <summary>
        /// Indicates that <b>neon-proxy-public</b> should reload its configuration.
        /// </summary>
        [JsonProperty(PropertyName = "PublicProxy", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool PublicProxy { get; set; } = false;

        /// <summary>
        /// Indicates that <b>neon-proxy-private</b> should reload its configuration.
        /// </summary>
        [JsonProperty(PropertyName = "PrivateProxy", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool PrivateProxy { get; set; } = false;

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

        /// <summary>
        /// Returns a human-readable summary of the message.
        /// </summary>
        /// <returns>The summary string.</returns>
        public override string ToString()
        {
            var sbPublicTargets  = new StringBuilder();
            var sbPrivateTargets = new StringBuilder();

            if (PublicProxy)
            {
                sbPublicTargets.AppendWithSeparator("proxy");
            }

            if (PublicBridge)
            {
                sbPublicTargets.AppendWithSeparator("bridge");
            }

            if (PublicCache)
            {
                sbPublicTargets.AppendWithSeparator("cache");
            }

            if (PrivateProxy)
            {
                sbPrivateTargets.AppendWithSeparator("proxy");
            }

            if (PrivateBridge)
            {
                sbPrivateTargets.AppendWithSeparator("bridge");
            }

            if (PrivateCache)
            {
                sbPrivateTargets.AppendWithSeparator("cache");
            }

            var sbTargets = new StringBuilder();

            if (sbPublicTargets.Length > 0)
            {
                sbTargets.Append($" [public: {sbPublicTargets}]");
            }

            if (sbPrivateTargets.Length > 0)
            {
                sbTargets.Append($" [private: {sbPrivateTargets}]");
            }

            return $"{nameof(ProxyUpdateMessage)}: [reason={Reason}]{sbTargets}";
        }
    }
}

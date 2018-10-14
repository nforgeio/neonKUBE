//-----------------------------------------------------------------------------
// FILE:	    ProxyRegenerateMessage.cs
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
    /// <b>neon-proxy-manager</b> that the proxy configuration has changed and
    /// that it should regenerate the configuration artifacts required by the
    /// other proxy related components.
    /// </summary>
    public class ProxyRegenerateMessage
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ProxyRegenerateMessage()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="reason">The human readable reason for the message.</param>
        public ProxyRegenerateMessage(string reason)
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
        /// Returns a human-readable summary of the message.
        /// </summary>
        /// <returns>The summary string.</returns>
        public override string ToString()
        {
            return $"{nameof(ProxyRegenerateMessage)}: [reason={Reason}]";
        }
    }
}

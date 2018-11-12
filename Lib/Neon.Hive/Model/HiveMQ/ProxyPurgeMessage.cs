//-----------------------------------------------------------------------------
// FILE:	    ProxyPurgeMessage.cs
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
    /// Broadcast to the <see cref="HiveMQChannels.ProxyNotify"/> channel 
    /// instructing any listening <b>neon-proxy-public-cache</b> or
    /// <b>neon-proxy-private-cache</b> instances to purge cached content
    /// that matches a <see cref="GlobPattern"/>.
    /// </summary>
    public class ProxyPurgeMessage
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ProxyPurgeMessage()
        {
        }

        /// <summary>
        /// The patterns to be used for purging content from the target cache.
        /// </summary>
        [JsonProperty(PropertyName = "PurgePatterns", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> PurgePatterns { get; set; } = new List<string>();

        /// <summary>
        /// Indicates that the content should be purged by <b>neon-proxy-public-cache</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PublicCache", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool PublicCache { get; set; } = false;

        /// <summary>
        /// Indicates that the content should be purged by <b>neon-proxy-private-cache</b>.
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
            var count = 0;

            if (PurgePatterns != null)
            {
                count = PurgePatterns.Count;
            }

            var target = (string)null;

            if (PublicCache)
            {
                target = "public-cache";
            }

            if (PrivateCache)
            {
                if (target == null)
                {
                    target = "private-cache";
                }
                else
                {
                    target += " private-cache";
                }
            }

            if (target == null)
            {
                target = "none";
            }

            return $"{nameof(ProxyPurgeMessage)}: [target={target}] [pattern-count={count}]";
        }
    }
}

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
using Neon.Net;

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
        //---------------------------------------------------------------------
        // Local types

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Describes a cache purge operation.  This can be either
        /// an origin specific operation where the cached responses for a specific origin 
        /// server are purged based on glob patterns.  Or you can create a specification
        /// that purges all cached responses.
        /// </summary>
        public class PurgeOperation
        {
            //-----------------------------------------------------------------
            // Static members

            /// <summary>
            /// Creates a purge operation for a specific origin server and one or
            /// more purge <see cref="GlobPattern"/> patterns.
            /// </summary>
            /// <param name="originHost">The origin hostname.</param>
            /// <param name="originPort">The origin port.</param>
            /// <param name="globPattern">The glob pattern to be used for purging content.</param>
            /// <returns>The <see cref="PurgeOperation"/>.</returns>
            public static PurgeOperation CreateOriginPurge(string originHost, int originPort, string globPattern)
            {
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(originHost));
                Covenant.Requires<ArgumentException>(NetHelper.IsValidPort(originPort));
                Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(globPattern));

                return new PurgeOperation()
                {
                    PurgeAll     = false,
                    OriginHost   = originHost,
                    OriginPort   = originPort,
                    UrlPattern = globPattern
                };
            }

            /// <summary>
            /// Creates a purge operation that purges all cached content.
            /// </summary>
            /// <returns>The <see cref="PurgeOperation"/>.</returns>
            public static PurgeOperation CreatePurgeAll()
            {
                return new PurgeOperation()
                {
                    PurgeAll = true
                };
            }

            //-----------------------------------------------------------------
            // Instance members

            /// <summary>
            /// Default constructor.
            /// </summary>
            public PurgeOperation()
            {
            }

            /// <summary>
            /// Identifies the origin hostname or <c>null</c> if <see cref="PurgeAll"/> is <c>true</c>.
            /// </summary>
            [JsonProperty(PropertyName = "OriginHost", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue(null)]
            public string OriginHost { get; set; }

            /// <summary>
            /// Identifies the origin port or <c>null</c> if <see cref="PurgeAll"/> is <c>true</c>.
            /// </summary>
            [JsonProperty(PropertyName = "OriginPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue(0)]
            public int OriginPort { get; set; }

            /// <summary>
            /// The glob pattern to be used for purging content from the target cache (when <see cref="PurgeAll"/> is <c>false</c>).
            /// </summary>
            [JsonProperty(PropertyName = "UrlPattern", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue(null)]
            public string UrlPattern { get; set; } = null;

            /// <summary>
            /// Indicates that the origin server hostname and port should be ignored and that all
            /// cached contents are to be purged.
            /// </summary>
            [JsonProperty(PropertyName = "PurgeAll", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
            [DefaultValue(false)]
            public bool PurgeAll { get; set; }

            /// <inheritdoc/>
            public override string ToString()
            {
                if (PurgeAll)
                {
                    return $"PURGE-ALL";
                }
                else
                {
                    return $"PURGE: origin-host={OriginHost}, origin-port={OriginPort}, uri-pattern={UrlPattern}";
                }
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ProxyPurgeMessage()
        {
        }

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
        /// Enables case sensitive URI pattern matching.
        /// </summary>
        [JsonProperty(PropertyName = "CaseSensitive", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool CaseSensitive { get; set; } = false;

        /// <summary>
        /// Lists the purge operations to be performed.
        /// </summary>
        [JsonProperty(PropertyName = "PurgeOperations", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<PurgeOperation> PurgeOperations { get; set;} = new List<PurgeOperation>();

        /// <summary>
        /// Adds an operation that purges content for a HAProxy frontend from the cache.
        /// </summary>
        /// <param name="frontendUri">
        /// The HAProxy frontend URI to be removed with optional <b>"*"</b> or <b>"**"</b> wildcards.
        /// </param>
        public void AddPurgeOrigin(string frontendUri)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(frontendUri));

            if (!Uri.TryCreate(frontendUri, UriKind.Absolute, out var uri))
            {
                throw new ArgumentException($"[{frontendUri}] is not a valid URI.");
            }

            var globPattern = uri.PathAndQuery;

            GlobPattern.Parse(globPattern);    // This validates the pattern.

            PurgeOperations.Add(PurgeOperation.CreateOriginPurge(uri.Host, uri.Port, uri.PathAndQuery));
        }

        /// <summary>
        /// Adds an operation that purges all cached content.
        /// </summary>
        public void AddPurgeAll()
        {
            PurgeOperations.Add(PurgeOperation.CreatePurgeAll());
        }

        /// <summary>
        /// Returns a human-readable summary of the message.
        /// </summary>
        /// <returns>The summary string.</returns>
        public override string ToString()
        {
            var count = 0;

            if (PurgeOperations != null)
            {
                count = PurgeOperations.Count;
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

            var purgeAll = string.Empty;

            if (PurgeOperations != null && !PurgeOperations.IsEmpty(o => o.PurgeAll))
            {
                purgeAll = "[PURGE-ALL] ";
            }

            return $"{nameof(ProxyPurgeMessage)}: [target={target}] {purgeAll}[pattern-count={count}]";
        }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    TrafficDirectorDefinition.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
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
    /// Holds the definition of a neonHIVE traffic director.
    /// </summary>
    public class TrafficDirectorDefinition
    {
        /// <summary>
        /// The traffic director name (currently <b>public</b> or <b>private</b>).
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        public string Name { get; set; }

        /// <summary>
        /// The traffic director settings.
        /// </summary>
        [JsonProperty(PropertyName = "Settings", Required = Required.Always)]
        public TrafficDirectorSettings Settings { get; set; }

        /// <summary>
        /// The traffic director rule dictionary keyed by name.
        /// </summary>
        [JsonProperty(PropertyName = "Rules", Required = Required.Always)]
        public Dictionary<string, TrafficDirectorRule> Rules { get; set; } = new Dictionary<string, TrafficDirectorRule>();

        /// <summary>
        /// Validates the traffic director definition.
        /// </summary>
        /// <param name="certificates">The dictionary of hive certificates keyed by name.</param>
        /// <returns>The <see cref="TrafficDirectorValidationContext"/>.</returns>
        public TrafficDirectorValidationContext Validate(Dictionary<string, TlsCertificate> certificates)
        {
            Covenant.Requires<ArgumentNullException>(certificates != null);

            var context = new TrafficDirectorValidationContext(Name, Settings, certificates);

            // Validate the existing settings and rules.

            Settings.Validate(context);

            foreach (var rule in Rules.Values)
            {
                rule.Validate(context);
            }

            // Verify that there are no existing frontend port/host conflicts:
            //
            //      * HTTP rules can share ports but hostnames must be unique.
            //      * HTTP rules on the same port cannot mix TLS and non-TLS.
            //      * Only one TCP port per rule is allowed.

            var httpMap       = new Dictionary<string, TrafficDirectorHttpRule>(StringComparer.OrdinalIgnoreCase);
            var httpPortToTls = new Dictionary<int, bool>();
            var tcpMap        = new Dictionary<int, TrafficDirectorTcpRule>();

            // Scan HTTP rules.

            foreach (var rule in Rules.Values.Where(r => r.Mode == TrafficDirectorMode.Http).OrderBy(r => r.Name.ToLowerInvariant()))
            {
                var httpRule = (TrafficDirectorHttpRule)rule;

                foreach (var frontend in httpRule.Frontends)
                {
                    var key = $"{frontend.Host}:{frontend.ProxyPort}";

                    if (!string.IsNullOrEmpty(frontend.PathPrefix))
                    {
                        key += frontend.PathPrefix;
                    }

                    if (httpMap.ContainsKey(key))
                    {
                        context.Error($"HTTP rule [{httpRule.Name}] has a frontend on [{key}] that conflicts with rule [{httpMap[key].Name}].");
                        continue;
                    }

                    httpMap.Add(key, httpRule);

                    if (!httpPortToTls.TryGetValue(frontend.ProxyPort, out var isTls))
                    {
                        isTls = frontend.Tls;
                        httpPortToTls.Add(frontend.ProxyPort, isTls);
                    }

                    if (isTls != frontend.Tls)
                    {
                        if (frontend.Tls)
                        {
                            context.Error($"HTTP rule [{httpRule.Name}] has a TLS frontend on port [{frontend.ProxyPort}] that conflicts with non-TLS frontends on this port.");
                        }
                        else
                        {
                            context.Error($"HTTP rule [{httpRule.Name}] has a non-TLS frontend on port [{frontend.ProxyPort}] that conflicts with TLS frontends on this port.");
                        }
                    }
                }
            }

            // Scan the TCP rules.

            foreach (var rule in Rules.Values.Where(r => r.Mode == TrafficDirectorMode.Tcp).OrderBy(r => r.Name.ToLowerInvariant()))
            {
                var tcpRule = (TrafficDirectorTcpRule)rule;

                foreach (var frontend in tcpRule.Frontends)
                {
                    var port = frontend.ProxyPort;

                    if (port == Settings.DefaultHttpPort || port == Settings.DefaultHttpsPort)
                    {
                        context.Error($"TCP rule [{tcpRule.Name}] has a frontend on [{port}] that conflicts the default traffic director HTTP or HTTPS port.");
                    }

                    if (httpPortToTls.ContainsKey(port))
                    {
                        context.Error($"TCP rule [{tcpRule.Name}] has a frontend on [{port}] that conflicts with one or more HTTP traffic director frontends on the same port.");
                    }

                    if (tcpMap.ContainsKey(port))
                    {
                        context.Error($"TCP rule [{tcpRule.Name}] has a frontend on [{port}] that conflicts with rule [{tcpMap[port].Name}].");
                    }

                    tcpMap.Add(port, tcpRule);
                }
            }

            return context;
        }
    }
}

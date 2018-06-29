//-----------------------------------------------------------------------------
// FILE:	    LoadBalancerDefinition.cs
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
    /// Holds the definition of a neonHIVE load balancer.
    /// </summary>
    public class LoadBalancerDefinition
    {
        /// <summary>
        /// The load balancer name (currently <b>public</b> or <b>private</b>).
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        public string Name { get; set; }

        /// <summary>
        /// The load balancer settings.
        /// </summary>
        [JsonProperty(PropertyName = "Settings", Required = Required.Always)]
        public LoadBalancerSettings Settings { get; set; }

        /// <summary>
        /// The load balancer rule dictionary keyed by name.
        /// </summary>
        [JsonProperty(PropertyName = "Rules", Required = Required.Always)]
        public Dictionary<string, LoadBalancerRule> Rules { get; set; } = new Dictionary<string, LoadBalancerRule>();

        /// <summary>
        /// Validates the load balancer definition.
        /// </summary>
        /// <param name="certificates">The dictionary of cluster certificates keyed by name.</param>
        /// <param name="addImplicitFrontends">Optionally add any implicit frontends (e.g. for HTTPS redirect).</param>
        /// <returns>The <see cref="LoadBalancerValidationContext"/>.</returns>
        public LoadBalancerValidationContext Validate(Dictionary<string, TlsCertificate> certificates, bool addImplicitFrontends = false)
        {
            Covenant.Requires<ArgumentNullException>(certificates != null);

            var context = new LoadBalancerValidationContext(Name, Settings, certificates);

            // Validate the existing settings and rules.

            Settings.Validate(context);

            foreach (var rule in Rules.Values)
            {
                rule.Validate(context, addImplicitFrontends: addImplicitFrontends);
            }

            // Verify that there are no existing frontend port/host conflicts:
            //
            //      * HTTP rules can share ports but hostnames must be unique.
            //      * HTTP rules on the same port cannot mix TLS and non-TLS.
            //      * Only one TCP port per rule is allowed.

            var httpMap       = new Dictionary<string, LoadBalancerHttpRule>(StringComparer.OrdinalIgnoreCase);
            var httpPortToTls = new Dictionary<int, bool>();
            var tcpMap        = new Dictionary<int, LoadBalancerTcpRule>();

            // Scan HTTP rules.

            foreach (var rule in Rules.Values.Where(r => r.Mode == LoadBalancerMode.Http).OrderBy(r => r.Name.ToLowerInvariant()))
            {
                var httpRule = (LoadBalancerHttpRule)rule;

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

            foreach (var rule in Rules.Values.Where(r => r.Mode == LoadBalancerMode.Tcp).OrderBy(r => r.Name.ToLowerInvariant()))
            {
                var tcpRule = (LoadBalancerTcpRule)rule;

                foreach (var frontend in tcpRule.Frontends)
                {
                    var port = frontend.ProxyPort;

                    if (port == Settings.DefaultHttpPort || port == Settings.DefaultHttpsPort)
                    {
                        context.Error($"TCP rule [{tcpRule.Name}] has a frontend on [{port}] that conflicts the default load balancer HTTP or HTTPS port.");
                    }

                    if (httpPortToTls.ContainsKey(port))
                    {
                        context.Error($"TCP rule [{tcpRule.Name}] has a frontend on [{port}] that conflicts with one or more HTTP load balancer frontends on the same port.");
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

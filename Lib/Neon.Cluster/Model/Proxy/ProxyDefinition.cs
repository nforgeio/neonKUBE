//-----------------------------------------------------------------------------
// FILE:	    ProxyDefinition.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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

namespace Neon.Cluster
{
    /// <summary>
    /// Holds the definition of a neonCLUSTER proxy.
    /// </summary>
    public class ProxyDefinition
    {
        /// <summary>
        /// The proxy name (currently <b>public</b> or <b>private</b>).
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Always)]
        public string Name { get; set; }

        /// <summary>
        /// The proxy settings.
        /// </summary>
        [JsonProperty(PropertyName = "Settings", Required = Required.Always)]
        public ProxySettings Settings { get; set; }

        /// <summary>
        /// The proxy route dictionary keyed by name.
        /// </summary>
        [JsonProperty(PropertyName = "Routes", Required = Required.Always)]
        public Dictionary<string, ProxyRoute> Routes { get; set; } = new Dictionary<string, ProxyRoute>();

        /// <summary>
        /// Validates the proxy definition.
        /// </summary>
        /// <param name="certificates">The dictionary of cluster certificates keyed by name.</param>
        /// <returns>The <see cref="ProxyValidationContext"/>.</returns>
        public ProxyValidationContext Validate(Dictionary<string, TlsCertificate> certificates)
        {
            Covenant.Requires<ArgumentNullException>(certificates != null);

            var context = new ProxyValidationContext(Name, Settings, certificates);

            // Validate the existing settings and routes.

            Settings.Validate(context);

            foreach (var route in Routes.Values)
            {
                route.Validate(context);
            }

            // Verify that there are no existing frontend port/host conflicts:
            //
            //      * HTTP routes can share ports but host names must be unique.
            //      * HTTP routes on the same port cannot mix TLS and non-TLS.
            //      * Only one TCP port per route is allowed.

            var httpMap       = new Dictionary<string, ProxyHttpRoute>(StringComparer.OrdinalIgnoreCase);
            var httpPortToTls = new Dictionary<int, bool>();
            var tcpMap        = new Dictionary<int, ProxyTcpRoute>();

            // Scan HTTP routes.

            foreach (var route in Routes.Values.Where(r => r.Mode == ProxyMode.Http).OrderBy(r => r.Name.ToLowerInvariant()))
            {
                var httpRoute = (ProxyHttpRoute)route;

                foreach (var frontend in httpRoute.Frontends)
                {
                    var key = $"{frontend.Host}:{frontend.ProxyPort}";

                    if (!string.IsNullOrEmpty(frontend.PathPrefix))
                    {
                        key += frontend.PathPrefix;
                    }

                    if (httpMap.ContainsKey(key))
                    {
                        context.Error($"HTTP route [{httpRoute.Name}] has a frontend on [{key}] that conflicts with route [{httpMap[key].Name}].");
                        continue;
                    }

                    httpMap.Add(key, httpRoute);

                    bool isTls;

                    if (!httpPortToTls.TryGetValue(frontend.ProxyPort, out isTls))
                    {
                        isTls = frontend.Tls;
                        httpPortToTls.Add(frontend.ProxyPort, isTls);
                    }

                    if (isTls != frontend.Tls)
                    {
                        if (frontend.Tls)
                        {
                            context.Error($"HTTP route [{httpRoute.Name}] has a TLS frontend on port [{frontend.ProxyPort}] that conflicts with non-TLS frontends on this port.");
                        }
                        else
                        {
                            context.Error($"HTTP route [{httpRoute.Name}] has a non-TLS frontend on port [{frontend.ProxyPort}] that conflicts with TLS frontends on this port.");
                        }
                    }
                }
            }

            // Scan the TCP routes.

            foreach (var route in Routes.Values.Where(r => r.Mode == ProxyMode.Tcp).OrderBy(r => r.Name.ToLowerInvariant()))
            {
                var tcpRoute = (ProxyTcpRoute)route;

                foreach (var frontend in tcpRoute.Frontends)
                {
                    var port = frontend.ProxyPort;

                    if (port == Settings.DefaultHttpPort || port == Settings.DefaultHttpsPort)
                    {
                        context.Error($"TCP route [{tcpRoute.Name}] has a frontend on [{port}] that conflicts the default proxy HTTP or HTTPS port.");
                    }

                    if (httpPortToTls.ContainsKey(port))
                    {
                        context.Error($"TCP route [{tcpRoute.Name}] has a frontend on [{port}] that conflicts with one or more HTTP proxy frontends on the same port.");
                    }

                    if (tcpMap.ContainsKey(port))
                    {
                        context.Error($"TCP route [{tcpRoute.Name}] has a frontend on [{port}] that conflicts with route [{tcpMap[port].Name}].");
                    }

                    tcpMap.Add(port, tcpRoute);
                }
            }

            return context;
        }
    }
}

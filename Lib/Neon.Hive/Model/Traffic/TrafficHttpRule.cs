//-----------------------------------------------------------------------------
// FILE:	    TrafficHttpRule.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Newtonsoft.Json;

namespace Neon.Hive
{
    /// <summary>
    /// Describes a traffic manager rule that forwards traffic from 
    /// HTTP and/or HTTPS frontends to HTTP backend servers.
    /// </summary>
    public class TrafficHttpRule : TrafficRule
    {
        private List<TrafficHttpBackend> selectedBackends;    // Used to cache selected backends

        /// <summary>
        /// Default constructor.
        /// </summary>
        public TrafficHttpRule()
        {
            base.Mode = TrafficMode.Http;
        }

        /// <summary>
        /// The traffic manager frontend definitions.
        /// </summary>
        [JsonProperty(PropertyName = "Frontends", Required = Required.Always)]
        public List<TrafficHttpFrontend> Frontends { get; set; } = new List<TrafficHttpFrontend>();

        /// <summary>
        /// The traffic manager backend definitions.
        /// </summary>
        [JsonProperty(PropertyName = "Backends", Required = Required.Always)]
        public List<TrafficHttpBackend> Backends { get; set; } = new List<TrafficHttpBackend>();

        /// <summary>
        /// <para>
        /// Describes how backend connections to origin servers may be reused for subsequent
        /// requests.  This maps directly to the <b>http-reuse </b>HAProxy and is discussed at length 
        /// <a href="https://cbonte.github.io/haproxy-dconv/1.8/configuration.html#4.2-http-reuse">here</a>.
        /// This defaults to <see cref="TrafficHttpReuse.Safe"/>.
        /// </para>
        /// <note>
        /// neonHIVE HTTP rules default to <see cref="TrafficHttpReuse.Safe"/> where as HAProxy defaults
        /// to <see cref="TrafficHttpReuse.Never"/>.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "BackendConnectionReuse", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(TrafficHttpReuse.Safe)]
        public TrafficHttpReuse BackendConnectionReuse { get; set; } = TrafficHttpReuse.Safe;

        /// <summary>
        /// HTTP caching related settings.  This defaults to <c>null</c> which disables any caching.
        /// </summary>
        [JsonProperty(PropertyName = "Cache", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public TrafficHttpCache Cache { get; set; }

        /// <summary>
        /// Returns the list of backends selected to be targeted by processing any
        /// backends with <see cref="TrafficBackend.Group"/> and <see cref="TrafficBackend.GroupLimit"/>
        /// properties configured to dynamically select backend target nodes.
        /// </summary>
        /// <param name="hostGroups">
        /// Dictionary mapping host group names to the list of host node 
        /// definitions within the named group.
        /// </param>
        /// <returns>The list of selected backends.</returns>
        /// <remarks>
        /// <note>
        /// This is a somewhat specialized method used by the <b>neon-proxy-manager</b>
        /// when generating HAProxy configuration files.
        /// </note>
        /// <note>
        /// This method will compute the select the first time it's called on an
        /// instance and then return the same selected backends thereafter.
        /// </note>
        /// </remarks>
        public List<TrafficHttpBackend> SelectBackends(Dictionary<string, List<NodeDefinition>> hostGroups)
        {
            Covenant.Requires<ArgumentNullException>(hostGroups != null);

            if (selectedBackends != null)
            {
                return selectedBackends;   // Return the cached backends
            }

            if (Backends.Count(be => !string.IsNullOrEmpty(be.Group)) == 0)
            {
                // There is no group targeting so we can just return the 
                // backend definitions as is.

                return Backends;
            }

            // We actually need to select backends.  Any backend that doesn't
            // target a group will be added as-is and then we'll need to
            // process group targets to actually select the backend nodes.
            //
            // Note that we're only going to process the first backend that
            // targets any given group (multiple backends targeting the 
            // same group will be considered to be a configuration problem).

            // NOTE:
            //
            // I'm treating a targeted host group that doesn't actually exist
            // as an empty group.  A case could be made to signal this as an
            // error or log a warning, but one could also argue that treating
            // this as a empty group makes logical sense (and it's much easier
            // to implement to boot).

            var processedGroups = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            selectedBackends = new List<TrafficHttpBackend>();

            foreach (var backend in Backends)
            {
                if (string.IsNullOrEmpty(backend.Group))
                {
                    selectedBackends.Add(backend);
                }
                else if (!processedGroups.Contains(backend.Group))
                {
                    foreach (var groupNode in backend.SelectGroupNodes(hostGroups).OrderBy(n => n.Name))
                    {
                        var backendClone = NeonHelper.JsonClone(backend);

                        backendClone.Name   = groupNode.Name;
                        backendClone.Server = groupNode.PrivateAddress.ToString();

                        selectedBackends.Add(backendClone);
                    }

                    processedGroups.Add(backend.Group);
                }
            }

            return selectedBackends;
        }

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Determines whether the rule has a single 
        /// backend that is reachable via a hostname lookup.
        /// </summary>
        /// <returns><c>true</c> for a single backend with a hostname.</returns>
        [JsonIgnore]
        [YamlIgnore]
        public bool HasSingleHostnameBackend
        {
            get
            {
                var hasHostname = false;

                foreach (var backend in Backends)
                {
                    if (!IPAddress.TryParse(backend.Server, out var address))
                    {
                        hasHostname = true;
                    }
                }

                return Backends.Count == 1 && hasHostname;
            }
        }

        /// <summary>
        /// Validates the rule.
        /// </summary>
        /// <param name="context">The validation context.</param>
        public override void Validate(TrafficValidationContext context)
        {
            base.Validate(context);

            Frontends = Frontends ?? new List<TrafficHttpFrontend>();
            Backends  = Backends ?? new List<TrafficHttpBackend>();

            if (Frontends.Count == 0)
            {
                context.Error($"Rule [{Name}] has does not define a frontend.");
            }

            if (!string.IsNullOrEmpty(CheckUri))
            {
                if (!Uri.TryCreate(CheckUri, UriKind.Relative, out var uri))
                {
                    context.Error($"Rule [{Name}] has invalid [{nameof(CheckUri)}={CheckUri}].");
                }
            }

            if (string.IsNullOrEmpty(CheckMethod) || CheckMethod.IndexOfAny(new char[] { ' ', '\r', '\n', '\t' }) != -1)
            {
                context.Error($"Rule [{Name}] has invalid [{nameof(CheckMethod)}={CheckMethod}].");
            }

            if (string.IsNullOrEmpty(CheckVersion))
            {
                CheckVersion = "1.0";
            }

            var regex = new Regex(@"^\d+\.\d+$");

            if (!regex.Match(CheckVersion).Success)
            {
                context.Error($"Rule [{Name}] has invalid [{nameof(CheckVersion)}={CheckVersion}].");
            }

            if (!string.IsNullOrEmpty(CheckHost) && !HiveDefinition.DnsHostRegex.Match(CheckHost).Success)
            {
                context.Error($"Rule [{Name}] has invalid [{nameof(CheckHost)}={CheckHost}].");
            }

            if (!string.IsNullOrEmpty(CheckExpect))
            {
                var error = $"Rule [{Name}] has invalid [{nameof(CheckExpect)}={CheckExpect}].";
                var value = CheckExpect.Trim();

                if (value.StartsWith("! "))
                {
                    value = value.Substring(2).Trim(); 
                }

                var pos = value.IndexOf(' ');

                if (pos == -1)
                {
                    context.Error(error + "  Expected: <match> <pattern>");
                }
                else
                {
                    var match   = value.Substring(0, pos);
                    var pattern = value.Substring(pos).Trim();

                    if (pattern.Replace("\\ ", string.Empty).IndexOf(' ') != -1)
                    {
                        context.Error(error + $"  Pattern [{pattern}] includes unescaped spaces.");
                    }

                    switch (match)
                    {
                        case "status":
                        case "string":

                            break;

                        case "rstatus":
                        case "rstring":

                            try
                            {
                                new Regex(pattern);
                            }
                            catch (Exception e)
                            {
                                context.Error(error + $"  Pattern regex [{pattern}] parsing error: {e.Message}.");
                            }
                            break;

                        default:

                            context.Error(error + "  Invalid [match], expected one of: status, rstatus, string, rstring");
                            break;
                    }
                }
            }

            foreach (var frontend in Frontends)
            {
                frontend.Validate(context, this);
            }

            foreach (var backend in Backends)
            {
                backend.Validate(context, this);
            }

            // Verify that the port/host combinations are unique for each frontend.

            var frontendMap = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var frontend in Frontends)
            {
                if (string.IsNullOrEmpty(frontend.PathPrefix))
                {
                    var key = $"{frontend.Host}:{frontend.ProxyPort}";

                    if (frontendMap.Contains(key))
                    {
                        context.Error($"HTTP rule [{Name}] includes two or more frontends that map to [{key}].");
                    }

                    frontendMap.Add(key);
                }
            }

            foreach (var frontend in Frontends)
            {
                if (!string.IsNullOrEmpty(frontend.PathPrefix))
                {
                    var key = $"{frontend.Host}:{frontend.ProxyPort}{frontend.PathPrefix}";

                    if (frontendMap.Contains($"{frontend.Host}:{frontend.ProxyPort}") ||    // Ensure there's no *all* path frontend
                        frontendMap.Contains(key))
                    {
                        context.Error($"HTTP rule [{Name}] includes two or more frontends that map to [{key}].");
                    }

                    frontendMap.Add(key);
                }
            }

            if (Cache != null && Cache.Enabled)
            {
                Cache.Validate(context, this);

                // The Varnish open source release doesn't support TLS backends.  This requires 
                // Varnish Plus (of course) which is very expensive.

                foreach (TrafficHttpBackend backend in Backends)
                {
                    if (backend.Tls)
                    {
                        context.Error($"HTTP rule [{Name}] cannot support caching because one or more backends required TLS.");
                        break;
                    }
                }

                // Varnish doesn't support comparing health probe status codes with a regex
                // like HAProxy does.  We're going to enforce having CheckExpect set to
                // something like "status 200".

                var statusFields = CheckExpect.Split(' ');

                if (statusFields.Length != 2 || statusFields[0] != "status" ||
                    !int.TryParse(statusFields[1], out var statusCode) ||
                    statusCode < 100 || 600 <= statusCode)
                {
                    context.Error($"HTTP rule [{Name}] cannot support caching because [{nameof(CheckExpect)}={CheckExpect}] doesn't specify a fixed status code like [status 200].  Varnish-Cache does not support verifying health probe status codes as regular expressions like HAProxy can.");
                }

                // $todo(jeff.lill):
                //
                // We need to enforce some restrictions due to Varnish limitations 
                // described here:
                //
                //      https://github.com/jefflill/NeonForge/issues/379
                //
                // It would be nice to revisit this in the future.

                // Ensure that:
                //
                //      * If one backend has a hostname then it must be the only backend.
                //      * IP address and hostname backends cannot be mixed.

                if (Backends.Count > 1)
                {
                    var hasHostname  = false;
                    var hasIPAddress = false;

                    foreach (var backend in Backends)
                    {
                        if (IPAddress.TryParse(backend.Server, out var address))
                        {
                            hasIPAddress = true;
                        }
                        else
                        {
                            hasHostname = true;
                        }
                    }

                    if (hasIPAddress)
                    {
                        context.Error($"HTTP rule [{Name}] has multiple backends reachable via hostname which is not supported.  You may define only a single backend that requires a DNS lookup.");
                    }
                    else if (hasIPAddress && hasHostname)
                    {
                        context.Error($"HTTP rule [{Name}] has backends reachable via IP address and hostname which is not supported.  You cannot mix backends with IP address and hostnames in the same rule.");
                    }
                }

                // Ensure that all cache warming targets have schemes, hostnames, ports that
                // match a rule frontend, and that HTTP rules don't map to reserved HTTPS ports 
                // and HTTPS rules don't map to reserved HTTP ports.

                foreach (var frontend in Frontends)
                {
                    if (frontend.Tls)
                    {
                        if (frontend.ProxyPort == HiveHostPorts.ProxyPublicHttp || frontend.ProxyPort == HiveHostPorts.ProxyPrivateHttp)
                        {
                            context.Error($"Rule [{Name}] has an HTTPS frontend with [{nameof(frontend.ProxyPort)}={frontend.ProxyPort}] that is incorrectly mapped to a reserved HTTP port.");
                        }
                    }
                    else
                    {
                        if (frontend.ProxyPort == HiveHostPorts.ProxyPublicHttps || frontend.ProxyPort == HiveHostPorts.ProxyPrivateHttps)
                        {
                            context.Error($"Rule [{Name}] has an HTTP frontend with [{nameof(frontend.ProxyPort)}={frontend.ProxyPort}] that is incorrectly mapped to a reserved HTTPS port.");
                        }
                    }
                }

                // Ensure that all cache warming targets have schemes, hostnames, and ports that
                // match a rule frontend.

                foreach (var warmTarget in Cache.WarmTargets)
                {
                    var uri = new Uri(warmTarget.Uri);
                    var tls = uri.Scheme.Equals("https", StringComparison.InvariantCultureIgnoreCase);

                    if (Frontends.IsEmpty(fe => fe.Tls == tls && fe.Host.Equals(uri.Host, StringComparison.InvariantCultureIgnoreCase) && fe.ProxyPort == uri.Port))
                    {
                        context.Error($"Cache warm target [{uri}] does not match one of the [{Name}] traffic manager frontends.");
                    }
                }
            }
        }

        /// <inheritdoc/>
        public override void Normalize(bool isPublic)
        {
            base.Normalize(isPublic);

            if (!isPublic)
            {
                foreach (var frontend in Frontends)
                {
                    frontend.PublicPort = 0;
                }
            }
        }

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Returns the frontend corresponding to a cache warming target URI.
        /// </summary>
        /// <param name="target">The warming target.</param>
        /// <returns>The corresponding <see cref="TrafficHttpFrontend"/>"/> or <c>null</c> if there's no match.</returns>
        public TrafficHttpFrontend GetFrontendForWarmTarget(TrafficWarmTarget target)
        {
            // We'll match frontends on scheme, hostname, port, and longest path prefix.
            //
            // Select the candidate frontends that match on scheme, hostname, and port:

            var uri        = new Uri(target.Uri);
            var tls        = uri.Scheme.Equals("https", StringComparison.InvariantCultureIgnoreCase);
            var candidates = Frontends.Where(fe => fe.Tls == tls && fe.Host.Equals(uri.Host, StringComparison.InvariantCultureIgnoreCase) && fe.ProxyPort == uri.Port).ToList();

            // We're done if there's no or only one candidate.

            if (candidates.Count == 0)
            {
                return null;
            }
            else if (candidates.Count == 1)
            {
                return candidates.Single();
            }

            // There's more than one candidate, so we'll try to match the frontend based on the
            // the path prefix, longest prefixes first.

            foreach (var frontend in candidates.OrderByDescending(fe => (fe.PathPrefix ?? string.Empty).Length))
            {
                var frontendPrefix = frontend.PathPrefix ?? string.Empty;

                if (uri.AbsolutePath.StartsWith(frontendPrefix))
                {
                    return frontend;
                }
            }

            // None of the prefixes matched.

            return null;
        }
    }
}

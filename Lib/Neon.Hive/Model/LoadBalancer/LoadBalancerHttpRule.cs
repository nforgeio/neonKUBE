//-----------------------------------------------------------------------------
// FILE:	    LoadBalancerHttpRule.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using Neon.Common;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text.RegularExpressions;

namespace Neon.Hive
{
    /// <summary>
    /// Describes a load balancer rule that forwards traffic from 
    /// HTTP and/or HTTPS frontends to HTTP backend servers.
    /// </summary>
    public class LoadBalancerHttpRule : LoadBalancerRule
    {
        private List<LoadBalancerHttpBackend> selectedBackends;    // Used to cache selected backends

        /// <summary>
        /// Default constructor.
        /// </summary>
        public LoadBalancerHttpRule()
        {
            base.Mode = LoadBalancerMode.Http;
        }

        /// <summary>
        /// <para>
        /// Indicates that HTTP requests should be redirected using HTTPS.
        /// This defaults to <c>false</c>.
        /// </para>
        /// <note>
        /// See the remarks for more details about how this works.
        /// </note>
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property works by implicitly adding an HTTP frontend for every defined
        /// HTTPS frontend (ones that specify a <see cref="LoadBalancerHttpFrontend.CertName"/>).
        /// and then having each of the HTTP frontends emit a <b>302 temporary redirect</b>,
        /// redirecting to the same URL with the <b>https://</b> scheme.
        /// </para>
        /// <note>
        /// This only works for rules added to the <b>public</b> load balancer on the <b>neon-public</b>
        /// network and it also works only for HTTP frontends with the port set to <b>0/80</b>
        /// and HTTPS frontends with the port set to <b>0/443</b>.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "HttpsRedirect", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool HttpsRedirect { get; set; } = false;

        /// <summary>
        /// The load balancer frontend definitions.
        /// </summary>
        [JsonProperty(PropertyName = "Frontends", Required = Required.Always)]
        public List<LoadBalancerHttpFrontend> Frontends { get; set; } = new List<LoadBalancerHttpFrontend>();

        /// <summary>
        /// The load balancer backend definitions.
        /// </summary>
        [JsonProperty(PropertyName = "Backends", Required = Required.Always)]
        public List<LoadBalancerHttpBackend> Backends { get; set; } = new List<LoadBalancerHttpBackend>();

        /// <summary>
        /// HTTP caching related settings.  This defaults to <c>null</c> which disables any caching.
        /// </summary>
        [JsonProperty(PropertyName = "Cache", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public LoadBalancerHttpCache Cache { get; set; }

        /// <summary>
        /// Returns the list of backends selected to be targeted by processing any
        /// backends with <see cref="LoadBalancerBackend.Group"/> and <see cref="LoadBalancerBackend.GroupLimit"/>
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
        public List<LoadBalancerHttpBackend> SelectBackends(Dictionary<string, List<NodeDefinition>> hostGroups)
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

            selectedBackends = new List<LoadBalancerHttpBackend>();

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
        /// Validates the rule.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <param name="addImplicitFrontends">Optionally add any implicit frontends (e.g. for HTTPS redirect).</param>
        public override void Validate(LoadBalancerValidationContext context, bool addImplicitFrontends = false)
        {
            base.Validate(context, addImplicitFrontends);

            Frontends = Frontends ?? new List<LoadBalancerHttpFrontend>();
            Backends  = Backends ?? new List<LoadBalancerHttpBackend>();

            if (HttpsRedirect)
            {
                if (!context.LoadBalancerName.Equals("public", StringComparison.InvariantCultureIgnoreCase))
                {
                    context.Error($"Rule [{Name}] has [{nameof(HttpsRedirect)}={HttpsRedirect}] defined for [{context.LoadBalancerName}] load balancer.  This is supported only for the [public] load balancer.");
                }

                if (addImplicitFrontends)
                {
                    //-------------------------------------------------------------
                    // This is where we implicitly add an HTTP rule for each HTTPS rule
                    // that redirects from the [http://] scheme to [https://].  We're 
                    // going to clone each HTTPS frontend to target the default port
                    // [0/80], and set [CertName=null] and then add this to the rule
                    // as the HTTP frontend, if it doesn't already exist.

                    // Create a set of the hosts for the HTTP frontends explicitly specified 
                    // by the hive operator that already target the default HTTP port so
                    // we can avoid overwriting any explicit frontends below.  These will
                    // be keyed by: [host/path]

                    var explicitHttpFrontends = new HashSet<string>();

                    foreach (LoadBalancerHttpFrontend httpFrontend in Frontends.Where(fe => !fe.Tls))
                    {
                        var hostAndPath = httpFrontend.HostAndPath;

                        if (httpFrontend.ProxyPort == 0 || httpFrontend.ProxyPort == HiveHostPorts.ProxyPublicHttp)
                        {
                            if (!explicitHttpFrontends.Contains(hostAndPath))
                            {
                                explicitHttpFrontends.Add(hostAndPath);
                            }
                        }
                    }

                    // Add an implicit HTTP frontend for each HTTPS frontend if an explicit
                    // HTTP frontend matching the [host/path] doesn't already exist.

                    var newHttpFrontends = new List<LoadBalancerHttpFrontend>();

                    foreach (var httpsFrontend in Frontends.Where(fe => fe.Tls))
                    {
                        if (explicitHttpFrontends.Contains(httpsFrontend.HostAndPath))
                        {
                            continue;   // Never overwrite an explicitly specified HTTP frontend.
                        }

                        if (httpsFrontend.ProxyPort == 0 || httpsFrontend.ProxyPort == HiveHostPorts.ProxyPublicHttps)
                        {
                            var clone = NeonHelper.JsonClone(httpsFrontend);

                            clone.CertName  = null;
                            clone.ProxyPort = 0;

                            newHttpFrontends.Add(clone);
                        }
                    }

                    foreach (var httpFrontend in newHttpFrontends)
                    {
                        this.Frontends.Add(httpFrontend);
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
                        context.Error($"Cache warm target [{uri}] does not match one of the [{Name}] load balancer frontends.");
                    }
                }
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

            if (Cache != null)
            {
                Cache.Validate(context);

                // The Varnish opensource release doesn't support TLS backends.  This requires 
                // Varnish Plus which is very expensive (of course).

                foreach (LoadBalancerHttpBackend backend in Backends)
                {
                    if (backend.Tls)
                    {
                        context.Error($"HTTP rule [{Name}] cannot support caching because one or more backends required TLS.");
                        break;
                    }
                }

                // Varnish doesn't support comparing health probe status codes with a regex
                // like HAProxy does.  We're going to enforce having CheckExpect set to
                // something like "string 200".

                var statusFields = CheckExpect.Split(' ');

                if (statusFields.Length != 2 || statusFields[0] != "string" ||
                    !int.TryParse(statusFields[1], out var statusCode) ||
                    statusCode < 100 || 600 <= statusCode)
                {
                    context.Error($"HTTP rule [{Name}] cannot support caching because [{nameof(CheckExpect)}={CheckExpect}] doesn't specify a single status code like [string 200].  Varnish-Cache does not support verifying health probe status codes as regular expressions like HAProxy can.");
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
    }
}

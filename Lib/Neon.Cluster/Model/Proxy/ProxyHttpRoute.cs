//-----------------------------------------------------------------------------
// FILE:	    ProxyHttpRoute.cs
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

namespace Neon.Cluster
{
    /// <summary>
    /// Describes a route that forwards traffic from HTTP and/or HTTPS frontends
    /// to HTTP backend servers.
    /// </summary>
    public class ProxyHttpRoute : ProxyRoute
    {
        private List<ProxyHttpBackend> selectedBackends;    // Used to cache selected backends

        /// <summary>
        /// Default constructor.
        /// </summary>
        public ProxyHttpRoute()
        {
            Mode = ProxyMode.Http;
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
        /// HTTPS frontend (ones that specify a <see cref="ProxyHttpFrontend.CertName"/>).
        /// and then having each of the HTTP frontends emit a <b>302 temporary redirect</b>,
        /// redirecting to the same URL with the <b>https://</b> scheme.
        /// </para>
        /// <note>
        /// This only works for routes added to the <b>public</b> proxy on the <b>neon-public</b>
        /// network and it also works only for HTTPS frontends with the port set to <b>0/5101</b>
        /// and HTTP frontends with the port set to <b>0/5100</b>.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "HttpsRedirect", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool HttpsRedirect { get; set; } = false;

        /// <summary>
        /// <para>
        /// The relative URI the proxy will use to verify the backend server health when <see cref="ProxyRoute.Check"/> is <c>true</c> .  
        /// The health check must return a <b>2xx</b> or <b>3xx</b> HTTP  status code to be considered healthy.  This defaults to the
        /// relative path <b>/</b>.  You can also set this to <c>null</c> or the empty string to disable HTTP based checks.
        /// </para>
        /// <para>
        /// You can also set this to <c>null</c> to enable simple TCP connect checks will be performed if <see cref="ProxyRoute.Check"/> 
        /// is enabled.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "CheckUri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("/")]
        public string CheckUri { get; set; } = "/";

        /// <summary>
        /// The HTTP method to be used when submitting backend server health requests.  This defaults to <b>HEAD</b>.
        /// </summary>
        [JsonProperty(PropertyName = "CheckMethod", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("HEAD")]
        public string CheckMethod { get; set; } = "HEAD";

        /// <summary>
        /// The HTTP version to be used for submitting backend checks.  This defaults to <b>1.0</b>.
        /// </summary>
        [JsonProperty(PropertyName = "CheckVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("1.0")]
        public string CheckVersion { get; set; } = "1.0";

        /// <summary>
        /// The HTTP <b>Host</b> header to be used when submitting the backend checks.  This
        /// defaults to <c>null</c>.  It's likely you'll need to specify this when setting
        /// <see cref="CheckVersion"/><b>="1.1"</b> since the host header is required by 
        /// the HTTP 1.1 specification.
        /// </summary>
        [JsonProperty(PropertyName = "CheckHost", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string CheckHost { get; set; } = null;

        /// <summary>
        /// <para>
        /// Specifies a response check that overrides the default <b>2xx</b>/<b>3xx</b> status code
        /// check.  This can be used to implement custom status code or response body checks.  This
        /// defaults to <c>null</c>.
        /// </para>
        /// <para>
        /// The property may be set to an expression implemented by the HAProxy 
        /// <a href="http://cbonte.github.io/haproxy-dconv/1.7/configuration.html#http-check%20expect">http-check expect</a> 
        /// keyword.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "CheckExpect", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string CheckExpect { get; set; } = null;

        /// <summary>
        /// The proxy frontend definitions.
        /// </summary>
        [JsonProperty(PropertyName = "Frontends", Required = Required.Always)]
        public List<ProxyHttpFrontend> Frontends { get; set; } = new List<ProxyHttpFrontend>();

        /// <summary>
        /// The proxy backend definitions.
        /// </summary>
        [JsonProperty(PropertyName = "Backends", Required = Required.Always)]
        public List<ProxyHttpBackend> Backends { get; set; } = new List<ProxyHttpBackend>();

        /// <summary>
        /// Returns the list of backends selected to be targeted by processing any
        /// backends with <see cref="ProxyBackend.Group"/> and <see cref="ProxyBackend.GroupLimit"/>
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
        public List<ProxyHttpBackend> SelectBackends(Dictionary<string, List<NodeDefinition>> hostGroups)
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
            // same group could be considered to be a configuration problem).

            // NOTE:
            //
            // I'm treating a targeted host group that doesn't actually exist
            // as an empty group.  A case could be made to signal this as an
            // error or log a warning, but one could also argue that treating
            // this as a empty group makes logical sense (and it's much easier
            // to implement to boot).

            var processedGroups = new HashSet<string>(StringComparer.InvariantCultureIgnoreCase);

            selectedBackends = new List<ProxyHttpBackend>();

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
        /// Validates the route.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <param name="addImplicitFrontends">Optionally add any implicit frontends (e.g. for HTTPS redirect).</param>
        public override void Validate(ProxyValidationContext context, bool addImplicitFrontends = false)
        {
            base.Validate(context, addImplicitFrontends);

            Frontends = Frontends ?? new List<ProxyHttpFrontend>();
            Backends  = Backends ?? new List<ProxyHttpBackend>();

            if (HttpsRedirect)
            {
                if (!context.ProxyName.Equals("public", StringComparison.InvariantCultureIgnoreCase))
                {
                    context.Error($"Route [{Name}] has [{nameof(HttpsRedirect)}={HttpsRedirect}] defined for [{context.ProxyName}] proxy.  This is supported only for the [public] proxy.");
                }

                if (addImplicitFrontends)
                {
                    //-------------------------------------------------------------
                    // This is where we implicitly add an HTTP route for each HTTPS route
                    // that redirects from the [http://] scheme to [https://].  We're 
                    // going to clone each HTTPS frontend that targets the default port
                    // [0/5100], and set [CertName=null] and then add this to the route
                    // as the HTTP frontend, if it doesn't already exist.

                    // Create a set of the hosts for the HTTP frontends explicitly specified 
                    // by the cluster operator that already target the default HTTP port so
                    // we can avoid overwriting any explicit frontends below.  These will
                    // be keyed by: [host/path]

                    var explicitHttpFrontends = new HashSet<string>();

                    foreach (ProxyHttpFrontend httpFrontend in Frontends.Where(fe => !fe.Tls))
                    {
                        var hostAndPath = httpFrontend.HostAndPath;

                        if (httpFrontend.ProxyPort == 0 || httpFrontend.ProxyPort == NeonHostPorts.ProxyPublicHttp)
                        {
                            if (!explicitHttpFrontends.Contains(hostAndPath))
                            {
                                explicitHttpFrontends.Add(hostAndPath);
                            }
                        }
                    }

                    // Add an implicit HTTP frontend for each HTTPS frontend if an explicit
                    // HTTP frontend matching the [host/path] doesn't already exist.

                    var newHttpFrontends = new List<ProxyHttpFrontend>();

                    foreach (var httpsFrontend in Frontends.Where(fe => fe.Tls))
                    {
                        if (explicitHttpFrontends.Contains(httpsFrontend.HostAndPath))
                        {
                            continue;   // Never overwrite an explicitly specified HTTP frontend.
                        }

                        if (httpsFrontend.ProxyPort == 0 || httpsFrontend.ProxyPort == NeonHostPorts.ProxyPublicHttps)
                        {
                            var clone = NeonHelper.JsonClone(httpsFrontend);

                            clone.CertName = null;
                            clone.ProxyPort = 0;

                            newHttpFrontends.Add(clone);
                        }
                    }

                    foreach (var httpFrontend in newHttpFrontends)
                    {
                        this.Frontends.Add(httpFrontend);
                    }
                }
            }

            if (!string.IsNullOrEmpty(CheckUri))
            {
                if (!Uri.TryCreate(CheckUri, UriKind.Relative, out var uri))
                {
                    context.Error($"Route [{Name}] has invalid [{nameof(CheckUri)}={CheckUri}].");
                }
            }

            if (string.IsNullOrEmpty(CheckMethod) || CheckMethod.IndexOfAny(new char[] { ' ', '\r', '\n', '\t' }) != -1)
            {
                context.Error($"Route [{Name}] has invalid [{nameof(CheckMethod)}={CheckMethod}].");
            }

            if (string.IsNullOrEmpty(CheckVersion))
            {
                CheckVersion = "1.0";
            }

            var regex = new Regex(@"^\d+\.\d+$");

            if (!regex.Match(CheckVersion).Success)
            {
                context.Error($"Route [{Name}] has invalid [{nameof(CheckVersion)}={CheckVersion}].");
            }

            if (!string.IsNullOrEmpty(CheckHost) && !ClusterDefinition.DnsHostRegex.Match(CheckHost).Success)
            {
                context.Error($"Route [{Name}] has invalid [{nameof(CheckHost)}={CheckHost}].");
            }

            if (!string.IsNullOrEmpty(CheckExpect))
            {
                var error = $"Route [{Name}] has invalid [{nameof(CheckExpect)}={CheckExpect}].";
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
                        context.Error($"HTTP route [{Name}] includes two or more frontends that map to [{key}].");
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
                        context.Error($"HTTP route [{Name}] includes two or more frontends that map to [{key}].");
                    }

                    frontendMap.Add(key);
                }
            }
        }
    }
}

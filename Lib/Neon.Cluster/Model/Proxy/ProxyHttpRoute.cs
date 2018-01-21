//-----------------------------------------------------------------------------
// FILE:	    ProxyHttpRoute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes a route that forwards traffic from HTTP and/or HTTPS frontends
    /// to HTTP backend servers.
    /// </summary>
    public class ProxyHttpRoute : ProxyRoute
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public ProxyHttpRoute()
        {
            Mode = ProxyMode.Http;
        }

        /// <summary>
        /// Indicates whether HTTP requests should be redirected using HTTPS.
        /// This defaults to <c>false</c>.
        /// </summary>
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
        public List<ProxyHttpFrontend> Frontends = new List<ProxyHttpFrontend>();

        /// <summary>
        /// The proxy backend definitions.
        /// </summary>
        [JsonProperty(PropertyName = "Backends", Required = Required.Always)]
        public List<ProxyHttpBackend> Backends = new List<ProxyHttpBackend>();

        /// <summary>
        /// Validates the route.
        /// </summary>
        /// <param name="context">The validation context.</param>
        public override void Validate(ProxyValidationContext context)
        {
            base.Validate(context);

            Frontends = Frontends ?? new List<ProxyHttpFrontend>();
            Backends  = Backends ?? new List<ProxyHttpBackend>();

            if (!string.IsNullOrEmpty(CheckUri))
            {
                Uri uri;

                if (!Uri.TryCreate(CheckUri, UriKind.Relative, out uri))
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

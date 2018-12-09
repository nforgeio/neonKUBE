//-----------------------------------------------------------------------------
// FILE:	    TrafficHttpFrontend.cs
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
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Cryptography;
using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// Describes an HTTP/HTTPS traffic manager frontend.
    /// </summary>
    public class TrafficHttpFrontend : TrafficFrontend
    {
        /// <summary>
        /// <para>
        /// The hostname to be matched for this frontend.
        /// </para>
        /// <note>
        /// This is required for rules targeting the traffic manager's default HTTP/S 
        /// ports or if the rule specifies more than one frontend.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Host", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Host { get; set; }

        /// <summary>
        /// <para>
        /// The optional relative URI path prefix to be matched for this frontend.
        /// </para>
        /// <note>
        /// This defaults to <c>null</c> indicating that all relative paths will be matched.
        /// Specifying an empty string has the same effect.
        /// </note>
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property can be used to direct traffic based on the URI path.  For example,
        /// you may wish to have different services implement parts of a REST API.  For example:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term>http://api.foo.com/v1/news</term>
        ///     <description>
        ///     news v1 service
        ///     </description>
        /// </item>
        /// <item>
        ///     <term>http://api.foo.com/v2/news</term>
        ///     <description>
        ///     news v2 service
        ///     </description>
        /// </item>
        /// <item>
        ///     <term>http://api.foo.com/v1/stocks</term>
        ///     <description>
        ///     stock service
        ///     </description>
        /// </item>
        /// <item>
        ///     <term>http://api.foo.com/v1/weather</term>
        ///     <description>
        ///     weather service
        ///     </description>
        /// </item>
        /// </list>
        /// <para>
        /// Here we expose a REST API sharing the same host: <b>api.foo.com</b> but actually deploy four
        /// separate services that implement different parts of the API.  This is convienent because you
        /// only need to maintain one host DNS record and certificate.  This also makes it easier to 
        /// integrate with content delivery networks.
        /// </para>
        /// <para>
        /// As you can see, I've split the API into news, stocks, and weather areas with distinct service
        /// implementations for each area as well as splitting news services by API version.  This feature
        /// fits in well with the Docker microservice concept.
        /// </para>
        /// <para>
        /// When specified, this property must begin with a forward slash (<b>/</b>) and will be implicitly
        /// terminated with a forward slash (<b>/</b>) if one isn't included.  Only valid URI characters
        /// are allowed.
        /// </para>
        /// <note>
        /// The traffic manager will match longer prefixes before shorter ones.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "PathPrefix", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string PathPrefix { get; set; }

        /// <summary>
        /// Optionally names the TLS certificate to be used to secure requests to the frontend.
        /// </summary>
        [JsonProperty(PropertyName = "CertName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string CertName { get; set; } = null;

        /// <summary>
        /// The optional HAProxy frontend port number.  This defaults to the traffic manager's default HTTPS port when a certificate name
        /// is specified or the default HTTP port otherwise.
        /// </summary>
        [JsonProperty(PropertyName = "ProxyPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(0)]
        public int ProxyPort { get; set; } = 0;

        /// <summary>
        /// <para>
        /// The network port to be exposed for this rule on the hive's public Internet facing load balancer.
        /// This defaults to <b>80</b> for HTTP rules or <b>443</b> for HTTPS rules.
        /// </para>
        /// <note>
        /// This is honored only for <b>public</b> traffic manager rules.  Public ports for <b>private</b> proxies will be ignored.
        /// </note>
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property's default value of <b>-1</b> is designed so that HTTP/HTTPS rules will <i>just work</i>, 
        /// by defaulting to their standard ports: <b>80</b> and <b>443</b>.  You can explicitly disable public 
        /// access for HTTP/HTTPS by setting this property to <b>0</b>.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "PublicPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(-1)]
        public int PublicPort { get; set; } = -1;

        /// <summary>
        /// <para>
        /// Optionally specifies that all requests hitting this frontend should be
        /// unconditionally redirected the scheme, host, and port specified by this URI.
        /// </para>
        /// <note>
        /// The path/query part of this URI is ignored.  The actual redirection will go to
        /// the specified scheme/host/port combined with the path from the original request.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "RedirectTo", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Uri RedirectTo { get; set; }

        /// <summary>
        /// Returns <c>true</c> if the frontend is to be secured via TLS.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool Tls
        {
            get { return !string.IsNullOrEmpty(CertName); }
        }

        /// <summary>
        /// Returns the frontend's host and path as a string.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        internal string HostAndPath
        {
            get
            {
                var hostAndPath = Host.ToLowerInvariant();

                if (!string.IsNullOrEmpty(PathPrefix))
                {
                    hostAndPath += PathPrefix;
                }
                else
                {
                    hostAndPath += "/";
                }

                return hostAndPath;
            }
        }

        /// <summary>
        /// <para>
        /// <b>INTERNAL USE ONLY:</b> Returns the value to be used as the <b>X-Neon-Frontend</b>
        /// header for requests being passed through to a <b>neon-proxy-cache</b> based service.
        /// This will look like one of:
        /// </para>
        /// <code>
        /// PORT PREFIX HOSTNAME
        /// PORT PREFIX
        /// </code>
        /// <para>
        /// where PORT is the HAProxy frontend port, PREFIX is the HTTP rule prefix if defined
        /// or "/" when not, and HOSTNAME optionally specifies the target hostname.
        /// </para>
        /// </summary>
        /// <returns>The <b>X-Neon-Frontend</b> header value for the frontend.</returns>
        public string GetProxyFrontendHeader()
        {
            var prefix = PathPrefix;

            if (string.IsNullOrEmpty(prefix))
            {
                prefix = "/";
            }

            if (!string.IsNullOrEmpty(Host))
            {
                return $"{ProxyPort} {prefix} {Host.ToLowerInvariant()}";
            }
            else
            {
                return $"{ProxyPort} {prefix}";
            }
        }

        /// <summary>
        /// Validates the frontend.
        /// </summary>
        /// <param name="context">The validation context.</param>
        /// <param name="rule">The parent rule.</param>
        public void Validate(TrafficValidationContext context, TrafficHttpRule rule)
        {
            base.Validate(context, rule);

            if (rule.Frontends.Count > 1 ||
                !string.IsNullOrEmpty(CertName) ||
                ProxyPort == 0 ||
                ProxyPort == HiveHostPorts.ProxyPublicHttp || ProxyPort == HiveHostPorts.ProxyPublicHttps ||
                ProxyPort == HiveHostPorts.ProxyPrivateHttp || ProxyPort == HiveHostPorts.ProxyPrivateHttps)
            {
                // The hostname is required so verify it.

                if (string.IsNullOrEmpty(Host))
                {
                    context.Error($"Rule [{rule.Name}] has a frontend without a [{nameof(Host)}] specified.  HTTP rules targeting the default traffic manager HTTP/S ports, with more than one frontend, or secured by TLS requires frontend hostnames.");
                }
                else if (!HiveDefinition.DnsHostRegex.IsMatch(Host))
                {
                    context.Error($"Rule [{rule.Name}] defines the invalid hostname [{Host}].");
                }
            }
            else
            {
                // The hostname is not required but verify it if one is specified.

                if (!string.IsNullOrEmpty(Host) && !HiveDefinition.DnsHostRegex.IsMatch(Host))
                {
                    context.Error($"Rule [{rule.Name}] defines the invalid hostname [{Host}].");
                }
            }

            if (!string.IsNullOrEmpty(PathPrefix))
            {
                if (!PathPrefix.StartsWith("/"))
                {
                    context.Error($"Rule [{rule.Name}] references has [{nameof(PathPrefix)}={PathPrefix}] that does not begin with a forward slash.");
                }
                else
                {
                    if (!PathPrefix.EndsWith("/"))
                    {
                        PathPrefix += "/";
                    }

                    if (!Uri.TryCreate(PathPrefix, UriKind.Relative, out Uri uri))
                    {
                        context.Error($"Rule [{rule.Name}] references has [{nameof(PathPrefix)}={PathPrefix}] that is not a valid relative URI.");
                    }
                }
            }

            if (CertName != null && context.ValidateCertificates)
            {
                TlsCertificate certificate;

                if (!context.Certificates.TryGetValue(CertName, out certificate))
                {
                    context.Error($"Rule [{rule.Name}] references certificate [{CertName}] that does not exist or could not be loaded.");
                }
                else
                {
                    if (!certificate.IsValidHost(Host))
                    {
                        context.Error($"Rule [{rule.Name}] references certificate [{CertName}] which does not cover host [{Host}].");
                    }

                    if (!certificate.IsValidDate(DateTime.UtcNow))
                    {
                        context.Error($"Rule [{rule.Name}] references certificate [{CertName}] which expired on [{certificate.ValidUntil}].");
                    }
                }
            }

            if (ProxyPort != 0)
            {
                if (!context.Settings.ProxyPorts.IsValidPort(ProxyPort))
                {
                    context.Error($"Rule [{rule.Name}] assigns [{nameof(ProxyPort)}={ProxyPort}] which is outside the range of valid frontend ports for this traffic manager [{context.Settings.ProxyPorts}].");
                }
            }
            else
            {
                if (CertName == null)
                {
                    ProxyPort = context.Settings.DefaultHttpPort;
                }
                else
                {
                    ProxyPort = context.Settings.DefaultHttpsPort;
                }
            }

            if (PublicPort == -1)
            {
                if (CertName == null)
                {
                    PublicPort = NetworkPorts.HTTP;
                }
                else
                {
                    PublicPort = NetworkPorts.HTTPS;
                }
            }

            if (PublicPort > 0 && !NetHelper.IsValidPort(PublicPort))
            {
                context.Error($"Load balancer [{nameof(PublicPort)}={PublicPort}] is not a valid network port.");
            }

            if (RedirectTo != null)
            {
                // Strip off the path/query part of the URI, if necessary.

                if (RedirectTo.PathAndQuery != "/")
                {
                    RedirectTo = new Uri($"{RedirectTo.Scheme}://{RedirectTo.Host}:{RedirectTo.Port}/");
                }
            }
        }
    }
}

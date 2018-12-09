//-----------------------------------------------------------------------------
// FILE:	    TrafficRule.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;

namespace Neon.Hive
{
    /// <summary>
    /// The base class for traffic manager rules.
    /// </summary>
    public class TrafficRule
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Identifies the built-in Docker DNS resolver.
        /// </summary>
        private const string defaultResolverName = "docker";

        /// <summary>
        /// Parses a <see cref="TrafficRule"/> from a JSON or YAML string,
        /// automatically detecting the input format.
        /// </summary>
        /// <param name="jsonOrYaml">The JSON or YAML input.</param>
        /// <param name="strict">Optionally require that all input properties map to rule properties.</param>
        /// <returns>The parsed object instance derived from <see cref="TrafficRule"/>.</returns>
        public static TrafficRule Parse(string jsonOrYaml, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(jsonOrYaml));

            if (jsonOrYaml.TrimStart().StartsWith("{"))
            {
                return ParseJson(jsonOrYaml, strict);
            }
            else
            {
                return ParseYaml(jsonOrYaml, strict);
            }
        }

        /// <summary>
        /// Parses a <see cref="TrafficRule"/> from a JSON string.
        /// </summary>
        /// <param name="jsonText">The input string.</param>
        /// <param name="strict">Optionally require that all input properties map to rule properties.</param>
        /// <returns>The parsed object instance derived from <see cref="TrafficRule"/>.</returns>
        public static TrafficRule ParseJson(string jsonText, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(jsonText));

            // We're going to ignore unmatched properties here because we
            // we're reading the base rule class first.

            var baseRule = NeonHelper.JsonDeserialize<TrafficRule>(jsonText);

            // Now that we know the rule mode, we can deserialize the full rule.

            switch (baseRule.Mode)
            {
                case TrafficMode.Http:

                    return NeonHelper.JsonDeserialize<TrafficHttpRule>(jsonText, strict);

                case TrafficMode.Tcp:

                    return NeonHelper.JsonDeserialize<TrafficTcpRule>(jsonText, strict);

                default:

                    throw new NotImplementedException($"Unsupported [{nameof(TrafficRule)}.{nameof(Mode)}={baseRule.Mode}].");
            }
        }

        /// <summary>
        /// Parses a <see cref="TrafficRule"/> from a YAML string.
        /// </summary>
        /// <param name="yamlText">The input string.</param>
        /// <param name="strict">Optionally require that all input properties map to rule properties.</param>
        /// <returns>The parsed object instance derived from <see cref="TrafficRule"/>.</returns>
        public static TrafficRule ParseYaml(string yamlText, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(yamlText));

            // We're going to ignore unmatched properties here because we
            // we're reading the base rule class first.

            var baseRule = NeonHelper.YamlDeserialize<TrafficRule>(yamlText, strict: false);

            // Now that we know the rule mode, we can deserialize the full rule.

            switch (baseRule.Mode)
            {
                case TrafficMode.Http:

                    return NeonHelper.YamlDeserialize<TrafficHttpRule>(yamlText, strict);

                case TrafficMode.Tcp:

                    return NeonHelper.YamlDeserialize<TrafficTcpRule>(yamlText, strict);

                default:

                    throw new NotImplementedException($"Unsupported [{nameof(TrafficRule)}.{nameof(Mode)}={baseRule.Mode}].");
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The rule name.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Name { get; set; } = null;

        /// <summary>
        /// Indicates whether HTTP or TCP traffic is to be handled (defaults to <see cref="TrafficMode.Http"/>).
        /// </summary>
        [JsonProperty(PropertyName = "Mode", Required = Required.Always)]
        [DefaultValue(TrafficMode.Http)]
        public TrafficMode Mode { get; set; } = TrafficMode.Http;

        /// <summary>
        /// <para>
        /// Identifies the DNS resolver to be used to lookup backend DNS names (defaults to the
        /// standard <b>docker</b> resolver for the attached networks).
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> This must be explicitly set to <c>null</c> or specify a non-Docker 
        /// resolver for containers or other services that are not attached to a Docker network.
        /// We defaulted this to <b>docker</b> because we expect the most traffic manager rules
        /// will be deployed for Docker services.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Resolver", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultResolverName)]
        public string Resolver { get; set; } = defaultResolverName;

        /// <summary>
        /// Enables network traffic logging.  This defaults to <c>true</c>.
        /// </summary>
        /// <remarks>
        /// <note>
        /// HTTP rules that share the same port will be logged if <b>any</b> of the rules
        /// on the port have logging enabled.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "Log", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool Log { get; set; } = true;

        /// <summary>
        /// Enables logging when backend server availability changes.  This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "LogChecks", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool LogChecks { get; set; } = true;

        /// <summary>
        /// Specifies the type of backend health checks to be performed.  This defaults to
        /// <see cref="TrafficCheckMode.Default"/> which will perform TCP connection 
        /// checks for <see cref="TrafficTcpRule"/> rules and HTTP checks for 
        /// <see cref="TrafficHttpRule"/> rules.
        /// </summary>
        /// <remarks>
        /// <para>
        /// You may set this to <see cref="TrafficCheckMode.Http"/> to perform HTTP
        /// checks for <see cref="TrafficTcpRule"/> rules or <see cref="TrafficCheckMode.Tcp"/>
        /// to perform TCP checks for <see cref="TrafficHttpRule"/> rules.
        /// </para>
        /// <para>
        /// You can also set this to <see cref="TrafficCheckMode.Disabled"/> to disable
        /// endpoint checking.  In this case, endpoints will always be considered healthy.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "Check", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(TrafficCheckMode.Default)]
        public TrafficCheckMode CheckMode { get; set; } = TrafficCheckMode.Default;

        /// <summary>
        /// Specifies the interval to wait between health checks.  This defaults to
        /// <b>5 seconds</b>.
        /// </summary>
        [JsonProperty(PropertyName = "CheckSeconds", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(5.0)]
        public double CheckSeconds { get; set; } = 5.0;

        /// <summary>
        /// Indicates that endpoint health check connections should be secured with TLS.
        /// This can be enabled  for both <see cref="TrafficHttpRule"/> and 
        /// <see cref="TrafficTcpRule"/> rules.  This defaults to <c>false</c>.
        /// </summary>
        [JsonProperty(PropertyName = "CheckTls", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool CheckTls { get; set; } = false;

        /// <summary>
        /// <para>
        /// The relative URI the traffic manager will use to verify the backend server health when <see cref="TrafficRule.CheckMode"/> is <c>true</c> .  
        /// The health check must return a <b>2xx</b> or <b>3xx</b> HTTP  status code to be considered healthy.  This defaults to the
        /// relative path <b>/</b>.  You can also set this to <c>null</c> or the empty string to disable HTTP based checks.
        /// </para>
        /// <para>
        /// You can also set this to <c>null</c> to enable simple TCP connect checks if <see cref="TrafficRule.CheckMode"/> 
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
        /// Specifies custom HTTP headers to be included with HTTP health checks submitted to
        /// the rule backends.
        /// </summary>
        [JsonProperty(PropertyName = "CheckHeaders", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<TrafficCheckHeader> CheckHeaders { get; set; } = new List<TrafficCheckHeader>();

        /// <summary>
        /// <para>
        /// Specifies a response check that overrides the default <b>2xx</b>/<b>3xx</b> status code
        /// check.  This can be used to implement custom status code or response body checks.  This
        /// defaults to <b>rstatus 2\d\d</b> to consider all 2xx status codes as healthy.
        /// </para>
        /// <para>
        /// The property may be set to an expression implemented by the HAProxy 
        /// <a href="http://cbonte.github.io/haproxy-dconv/1.7/configuration.html#http-check%20expect">http-check expect</a> 
        /// keyword.
        /// </para>
        /// <note>
        /// <para>
        /// <b>IMPORTANT:</b> When caching is enabled for a rule, the built-in Varnish cache will 
        /// only honor <see cref="CheckExpect"/> expressions like <b>status 200</b> when probing 
        /// backend servers because Varnish supports only a fixed status codes.
        /// </para>
        /// <para>
        /// The workaround is to use specialized health check endpoint that is guaranteed to return
        /// a specific status code when it's healthy.  This is generally a good best practice rather
        /// than using something like a site home page as the health endpoint.
        /// </para>
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "CheckExpect", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("rstatus 2\\d\\d")]
        public string CheckExpect { get; set; } = "rstatus 2\\d\\d";

        /// <summary>
        /// The endpoint timeouts.  These default to standard timeouts defined by <see cref="TrafficTimeouts"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Timeouts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public TrafficTimeouts Timeouts { get; set; } = null;

        /// <summary>
        /// Optionally indicates that this is to be considered to be a <b>system</b> rule
        /// used to support the underlying neonHIVE as opposed to a regular user defined
        /// rule.  This has no impact other than the fact that system rules are hidden
        /// by <b>neon-cli</b> commands by default.
        /// </summary>
        [JsonProperty(PropertyName = "System", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool System { get; set; } = false;

        /// <summary>
        /// Returns <c>true</c> if the rule performs HTTP backend checks.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool UseHttpCheckMode
        {
            get
            {
                switch (CheckMode)
                {
                    case TrafficCheckMode.Default:  return this is TrafficHttpRule;
                    case TrafficCheckMode.Disabled: return false;
                    case TrafficCheckMode.Http:     return true;
                    case TrafficCheckMode.Tcp:      return false;
                    default:                               throw new NotImplementedException($"Unexpected traffic manager [{nameof(CheckMode)}={CheckMode}].");
                }
            }
        }

        /// <summary>
        /// Validates the instance.
        /// </summary>
        /// <param name="context">The validation context.</param>
        public virtual void Validate(TrafficValidationContext context)
        {
            Timeouts = Timeouts ?? new TrafficTimeouts();

            if (string.IsNullOrEmpty(Name))
            {
                context.Error($"Load balancer rule name is required.");
            }

            if (!HiveDefinition.NameRegex.IsMatch(Name))
            {
                context.Error($"Load balancer rule name [{nameof(Name)}={Name}] is not valid.");
            }

            if (context.ValidateResolvers)
            {
                if (!string.IsNullOrWhiteSpace(Resolver) &&
                    context.Settings != null &&
                    context.Settings.Resolvers.Count(r => string.Equals(Resolver, r.Name, StringComparison.OrdinalIgnoreCase)) == 0)
                {
                    context.Error($"Load balancer resolver [{nameof(Resolver)}={Resolver}] does not exist.");
                }
            }

            Resolver     = Resolver ?? defaultResolverName;
            CheckHeaders = CheckHeaders ?? new List<TrafficCheckHeader>();

            foreach (var checkHeader in CheckHeaders)
            {
                checkHeader.Validate(context, this);
            }

            if (UseHttpCheckMode)
            {
                if (string.IsNullOrEmpty(CheckUri) || !Uri.TryCreate(CheckUri, UriKind.RelativeOrAbsolute, out var uri))
                {
                    context.Error($"Rule [{nameof(Name)}] has invalid [{nameof(CheckUri)}={CheckUri}].");
                }
            }

            if (CheckSeconds < 0.0)
            {
                CheckSeconds = 5.0;
            }

            Timeouts.Validate(context);
        }

        /// <summary>
        /// Normalizes the rule by clearing any unnecessary properties.
        /// </summary>
        /// <param name="isPublic">Indicates that this is a <b>public</b> rule.</param>
        public virtual void Normalize(bool isPublic)
        {
            if (CheckHeaders?.Count > 0)
            {
                CheckHeaders = null;
            }
        }

        /// <summary>
        /// Renders the rule as JSON.
        /// </summary>
        /// <returns>JSON text.</returns>
        public string ToJson()
        {
            return NeonHelper.JsonSerialize(this, Formatting.Indented);
        }

        /// <summary>
        /// Renders the rule as YAML.
        /// </summary>
        /// <returns>YAML text.</returns>
        public string ToYaml()
        {
            return NeonHelper.YamlSerialize(this);
        }
    }
}

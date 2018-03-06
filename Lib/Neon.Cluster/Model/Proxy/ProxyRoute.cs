//-----------------------------------------------------------------------------
// FILE:	    ProxyRoute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Dynamic;
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
    /// The base class for proxy routes.
    /// </summary>
    public class ProxyRoute
    {
        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Identifies the built-in Docker DNS resolver.
        /// </summary>
        private const string defaultResolverName = "docker";

        /// <summary>
        /// Parses a <see cref="ProxyRoute"/> from a JSON or YAML string,
        /// automatically detecting the input format.
        /// </summary>
        /// <param name="jsonOrYaml">The JSON or YAML input.</param>
        /// <param name="strict">Optionally require that all input properties map to route properties.</param>
        /// <returns>The parsed object instance derived from <see cref="ProxyRoute"/>.</returns>
        public static ProxyRoute Parse(string jsonOrYaml, bool strict = false)
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
        /// Parses a <see cref="ProxyRoute"/> from a JSON string.
        /// </summary>
        /// <param name="jsonText">The input string.</param>
        /// <param name="strict">Optionally require that all input properties map to route properties.</param>
        /// <returns>The parsed object instance derived from <see cref="ProxyRoute"/>.</returns>
        public static ProxyRoute ParseJson(string jsonText, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(jsonText));

            // We're going to ignore unmatched properties here because we
            // we're reading the base route class first.

            var baseRoute = NeonHelper.JsonDeserialize<ProxyRoute>(jsonText);

            // Now that we know the route mode, we can deserialize the full route.

            switch (baseRoute.Mode)
            {
                case ProxyMode.Http:

                    return NeonHelper.JsonDeserialize<ProxyHttpRoute>(jsonText, strict);

                case ProxyMode.Tcp:

                    return NeonHelper.JsonDeserialize<ProxyTcpRoute>(jsonText, strict);

                default:

                    throw new NotImplementedException($"Unsupported [{nameof(ProxyRoute)}.{nameof(Mode)}={baseRoute.Mode}].");
            }
        }

        /// <summary>
        /// Parses a <see cref="ProxyRoute"/> from a YAML string.
        /// </summary>
        /// <param name="yamlText">The input string.</param>
        /// <param name="strict">Optionally require that all input properties map to route properties.</param>
        /// <returns>The parsed object instance derived from <see cref="ProxyRoute"/>.</returns>
        public static ProxyRoute ParseYaml(string yamlText, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(yamlText));

            // We're going to ignore unmatched properties here because we
            // we're reading the base route class first.

            var baseRoute = NeonHelper.YamlDeserialize<ProxyRoute>(yamlText, strict: false);

            // Now that we know the route mode, we can deserialize the full route.

            switch (baseRoute.Mode)
            {
                case ProxyMode.Http:

                    return NeonHelper.YamlDeserialize<ProxyHttpRoute>(yamlText, strict);

                case ProxyMode.Tcp:

                    return NeonHelper.YamlDeserialize<ProxyTcpRoute>(yamlText, strict);

                default:

                    throw new NotImplementedException($"Unsupported [{nameof(ProxyRoute)}.{nameof(Mode)}={baseRoute.Mode}].");
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// The route name.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Name { get; set; } = null;

        /// <summary>
        /// Indicates whether HTTP or TCP traffic is to be handled (defaults to <see cref="ProxyMode.Http"/>).
        /// </summary>
        [JsonProperty(PropertyName = "Mode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(ProxyMode.Http)]
        public ProxyMode Mode { get; set; } = ProxyMode.Http;

        /// <summary>
        /// <para>
        /// Identifies the DNS resolver to be used to lookup backend DNS names (defaults to the
        /// standard <b>docker</b> resolver for the attached networks).
        /// </para>
        /// <note>
        /// <b>IMPORTANT:</b> This must be explicitly set to <c>null</c> or specify a non-Docker 
        /// resolver for containers or other services that are not attached to a Docker network.
        /// We defaulted this to <b>docker</b> because we expect the most proxy routes will be
        /// deployed for Docker services.
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
        /// HTTP routes that share the same port will be logged if <b>any</b> of the routes
        /// on the port have logging enabled.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "Log", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool Log { get; set; } = true;

        /// <summary>
        /// Enables backend server health checks.  This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Check", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool Check { get; set; } = true;

        /// <summary>
        /// Enables logging when backend server availability changes.  This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "LogChecks", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool LogChecks { get; set; } = true;

        /// <summary>
        /// Validates the instance.
        /// </summary>
        /// <param name="context">The validation context.</param>
        public virtual void Validate(ProxyValidationContext context)
        {
            if (string.IsNullOrEmpty(Name))
            {
                context.Error($"Proxy route name is required.");
            }

            if (!ClusterDefinition.NameRegex.IsMatch(Name))
            {
                context.Error($"Proxy route name [{nameof(Name)}={Name}] is not valid.");
            }

            if (!string.IsNullOrWhiteSpace(Resolver) && context.Settings.Resolvers.Count(r => string.Equals(Resolver, r.Name, StringComparison.OrdinalIgnoreCase)) == 0)
            {
                context.Error($"Proxy resolver [{nameof(Resolver)}={Resolver}] does not exist.");
            }
        }

        /// <summary>
        /// Renders the route as JSON.
        /// </summary>
        /// <returns>JSON text.</returns>
        public string ToJson()
        {
            return NeonHelper.JsonSerialize(this, Formatting.Indented);
        }

        /// <summary>
        /// Renders the route as YAML.
        /// </summary>
        /// <returns>YAML text.</returns>
        public string ToYaml()
        {
            return NeonHelper.YamlSerialize(this);
        }
    }
}

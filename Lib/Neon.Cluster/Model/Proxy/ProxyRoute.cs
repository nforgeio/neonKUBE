//-----------------------------------------------------------------------------
// FILE:	    ProxyRoute.cs
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
using YamlDotNet.Serialization.NamingConventions;

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

        private const string defaultResolverName = "docker";

        /// <summary>
        /// Parses a <see cref="ProxyRoute"/> from a JSON string.
        /// </summary>
        /// <param name="jsonText">The input string.</param>
        /// <returns>The parsed object instance derived from <see cref="ProxyRoute"/>.</returns>
        public static ProxyRoute ParseJson(string jsonText)
        {
            var baseRoute = NeonHelper.JsonDeserialize<ProxyRoute>(jsonText);

            switch (baseRoute.Mode)
            {
                case ProxyMode.Http:

                    return NeonHelper.JsonDeserialize<ProxyHttpRoute>(jsonText);

                case ProxyMode.Tcp:

                    return NeonHelper.JsonDeserialize<ProxyTcpRoute>(jsonText);

                default:

                    throw new NotImplementedException($"Unsupported [{nameof(ProxyRoute)}.{nameof(Mode)}={baseRoute.Mode}].");
            }
        }

        /// <summary>
        /// Parses a <see cref="ProxyRoute"/> from a YAML string.
        /// </summary>
        /// <param name="yamlText">The input string.</param>
        /// <returns>The parsed object instance derived from <see cref="ProxyRoute"/>.</returns>
        /// <remarks>
        /// <note>
        /// This YAML parser does not enforce property name case sensitivity.
        /// </note>
        /// </remarks>
        public static ProxyRoute ParseYaml(string yamlText)
        {
            // Note that for YAML we're NOT going to enforce property name
            // capitalization.  The [HyphenatedNamingConvention] instance
            // appears to relax case sensitivity.

            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(new HyphenatedNamingConvention())
                .Build();

            var baseRoute = deserializer.Deserialize<ProxyRoute>(yamlText);

            switch (baseRoute.Mode)
            {
                case ProxyMode.Http:

                    return deserializer.Deserialize<ProxyHttpRoute>(yamlText);

                case ProxyMode.Tcp:

                    return deserializer.Deserialize<ProxyTcpRoute>(yamlText);

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
    }
}

//-----------------------------------------------------------------------------
// FILE:	    ProxySettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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
using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes the global settings for a NeonCluster proxy.
    /// </summary>
    public class ProxySettings
    {
        private const int defaultMaxConnections = 32000;

        /// <summary>
        /// First reserved port on the Docker ingress network in the block allocated to this proxy.
        /// </summary>
        [JsonProperty(PropertyName = "FirstPort", Required = Required.Always)]
        public int FirstPort { get; set; } = 0;

        /// <summary>
        /// Last reserved port on the Docker ingress network in the block allocated to this proxy.
        /// </summary>
        [JsonProperty(PropertyName = "LastPort", Required = Required.Always)]
        public int LastPort { get; set; } = 0;

        /// <summary>
        /// Returns the standard reserved HTTP port.
        /// </summary>
        [JsonIgnore]
        public int DefaultHttpPort
        {
            get { return FirstPort; }
        }

        /// <summary>
        /// Returns the standard reserved HTTPS port.
        /// </summary>
        [JsonIgnore]
        public int DefaultHttpsPort
        {
            get { return FirstPort + 1; }
        }

        /// <summary>
        /// Returns the first possible TCP port.
        /// </summary>
        [JsonIgnore]
        public int FirstTcpPort
        {
            get { return FirstPort + 2; }
        }

        /// <summary>
        /// The maximum overall number of simultaneous inbound connections 
        /// to be allowed for the proxy.  (Defaults to <b>32000</b>).
        /// </summary>
        [JsonProperty(PropertyName = "MaxConnections", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultMaxConnections)]
        public int MaxConnections { get; set; } = defaultMaxConnections;

        /// <summary>
        /// The default endpoint timeouts.
        /// </summary>
        [JsonProperty(PropertyName = "Timeouts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public ProxyTimeouts Timeouts { get; set; } = new ProxyTimeouts();

        /// <summary>
        /// <para>
        /// The proxy's DNS resolvers.
        /// </para>
        /// <note>
        /// This includes the standard <b>docker</b> resolver by default, which 
        /// provides for dynamic service resolution on the attached networks.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Resolvers")]
        public List<ProxyResolver> Resolvers { get; set; } = new List<ProxyResolver>();

        /// <summary>
        /// <para>
        /// The maximum number of Diffie-Hellman parameters used for generating
        /// the ephemeral/temporary Diffie-Hellman key in case of DHE key exchange.
        /// </para>
        /// <para>
        /// Valid values are <b>1024</b>, <b>2048</b>, and <b>4096</b>.  The default
        /// value is <b>2048</b>.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "MaxDHParamBits", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(2048)]
        public int MaxDHParamBits { get; set; } = 2048;

        /// <summary>
        /// Validates the instance.
        /// </summary>
        /// <param name="context">The validation context.</param>
        public void Validate(ProxyValidationContext context)
        {
            Timeouts  = Timeouts ?? new ProxyTimeouts();
            Resolvers = Resolvers ?? new List<ProxyResolver>();

            if (!Resolvers.Exists(r => r.Name == "docker"))
            {
                Resolvers.Add(
                    new ProxyResolver()
                    {
                        Name = "docker",
                        NameServers = new List<ProxyNameserver>()
                        {
                            new ProxyNameserver()
                            {
                                Name     = "docker0",
                                Endpoint = NeonClusterConst.DockerDnsEndpoint
                            }
                        }
                    });
            }

            if (!NetHelper.IsValidPort(FirstPort) ||
                !NetHelper.IsValidPort(LastPort) ||
                LastPort <= FirstPort + 1)
            {
                context.Error($"Proxy port block [{FirstPort}-{LastPort}] range is not valid.");
            }

            if (MaxConnections <= 0)
            {
                context.Error($"Proxy settings [{nameof(MaxConnections)}={MaxConnections}] is not positive.");
            }

            Timeouts.Validate(context);

            if (!Resolvers.Exists(r => r.Name == "docker"))
            {
                context.Error($"Proxy settings [{nameof(Resolvers)}] must include a [docker] definition.");
            }

            foreach (var resolver in Resolvers)
            {
                resolver.Validate(context);
            }
        }
    }
}

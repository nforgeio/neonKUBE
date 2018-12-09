//-----------------------------------------------------------------------------
// FILE:	    TrafficSettings.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Hive
{
    /// <summary>
    /// Describes the global settings for a neonHIVE traffic manager.
    /// </summary>
    public class TrafficSettings
    {
        //---------------------------------------------------------------------
        // Static members

        private const int defaultMaxConnections = 32000;
        private const int defaultSslCacheSize   = 100000;

        /// <summary>
        /// Parses a <see cref="TrafficSettings"/> from a JSON or YAML string,
        /// automatically detecting the input format.
        /// </summary>
        /// <param name="jsonOrYaml">The JSON or YAML input.</param>
        /// <param name="strict">Optionally require that all input properties map to settings properties.</param>
        /// <returns>The parsed <see cref="TrafficSettings"/>.</returns>
        public static TrafficSettings Parse(string jsonOrYaml, bool strict = false)
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
        /// <param name="strict">Optionally require that all input properties map to settings properties.</param>
        /// <returns>The parsed <see cref="TrafficSettings"/>.</returns>
        public static TrafficSettings ParseJson(string jsonText, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(jsonText));

            return NeonHelper.JsonDeserialize<TrafficSettings>(jsonText, strict);
        }

        /// <summary>
        /// Parses a <see cref="TrafficRule"/> from a YAML string.
        /// </summary>
        /// <param name="yamlText">The input string.</param>
        /// <param name="strict">Optionally require that all input properties map to settings properties.</param>
        /// <returns>The parsed <see cref="TrafficSettings"/>.</returns>
        public static TrafficSettings ParseYaml(string yamlText, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(yamlText));

            return NeonHelper.YamlDeserialize<TrafficSettings>(yamlText, strict);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Describes the individual ports and port ranges exposed by a hive proxy.
        /// </summary>
        [JsonProperty(PropertyName = "ProxyPorts", Required = Required.Always)]
        public HiveProxyPorts ProxyPorts { get; set; }

        /// <summary>
        /// Returns the standard reserved HTTP port.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public int DefaultHttpPort
        {
            get
            {
                if (ProxyPorts.Ports.Contains(80))
                {
                    return 80;
                }

                return ProxyPorts.PortRange.FirstPort;
            }
        }

        /// <summary>
        /// Returns the standard reserved HTTPS port.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public int DefaultHttpsPort
        {
            get
            {
                if (ProxyPorts.Ports.Contains(443))
                {
                    return 443;
                }

                return ProxyPorts.PortRange.FirstPort + 1;
            }
        }

        /// <summary>
        /// Returns the first possible TCP port.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public int FirstTcpPort
        {
            get
            {
                if (ProxyPorts.Ports.Contains(80) && ProxyPorts.Ports.Contains(443))
                {
                    return ProxyPorts.PortRange.FirstPort;
                }
                else if (!ProxyPorts.Ports.Contains(80) && !ProxyPorts.Ports.Contains(443))
                {
                    return ProxyPorts.PortRange.FirstPort + 2;
                }
                else
                {
                    Covenant.Assert(false, "Unexpected traffic manager configuration.");
                    throw new InvalidOperationException();
                }
            }
        }

        /// <summary>
        /// The maximum overall number of simultaneous inbound connections 
        /// to be allowed for the traffic manager.  (Defaults to <b>32000</b>).
        /// </summary>
        [JsonProperty(PropertyName = "MaxConnections", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultMaxConnections)]
        public int MaxConnections { get; set; } = defaultMaxConnections;

        /// <summary>
        /// The maximum number of SSL handshakes that can be cached.  This helps speed
        /// subsequent client SSL connections because we can avoid the CPU intensive
        /// cryptographic operations.  Note that each cached handshake will consume
        /// about 200 bytes or RAM.  This defaults to <b>100000</b> entries or about 
        /// 20 MiB of RAM.
        /// </summary>
        [JsonProperty(PropertyName = "SslCacheSize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultSslCacheSize)]
        public int SslCacheSize { get; set; } = defaultSslCacheSize;

        /// <summary>
        /// The default endpoint timeouts.
        /// </summary>
        [JsonProperty(PropertyName = "Timeouts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public TrafficTimeouts Timeouts { get; set; } = new TrafficTimeouts();

        /// <summary>
        /// <para>
        /// The DNS resolvers available for use by the traffic manager's rules.
        /// </para>
        /// <note>
        /// This includes the standard <b>docker</b> resolver by default, which 
        /// provides for dynamic service resolution on the attached networks.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Resolvers")]
        public List<TrafficResolver> Resolvers { get; set; } = new List<TrafficResolver>();

        /// <summary>
        /// <para>
        /// The maximum number of Diffie-Hellman parameters used for generating
        /// the ephemeral/temporary Diffie-Hellman key in case of DHE key exchange.
        /// </para>
        /// <para>
        /// Valid values are <b>1024</b>, <b>2048</b>, and <b>4096</b>.  The default
        /// is <b>2048</b>.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "MaxDHParamBits", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(2048)]
        public int MaxDHParamBits { get; set; } = 2048;

        /// <summary>
        /// Specifies the desired number of Swarm nodes to be designated as bridge
        /// traffic manager targets.  This can be overridden by explicity designating 
        /// target node IP addresses in <see cref="BridgeTargetAddresses"/>.  This
        /// defaults to <b>5</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the number of active workers equals this count then all of them will
        /// be designated as bridge traffic manager targets.  If there are more workers, then
        /// <see cref="BridgeTargetCount"/> workers will be randomly selected as
        /// targets.  If there are fewer workers, then all active Swarm nodes 
        /// (including managers) will be targeted.
        /// </para>
        /// <para>
        /// It is also possible to explicity specify that bridge targets via
        /// <see cref="BridgeTargetAddresses"/>.  That property overrides this
        /// one.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "BridgeTargetCount", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(5)]
        public int BridgeTargetCount { get; set; } = 5;

        /// <summary>
        /// Explicitly specifies the bridge target Swarm nodes by IP address.  This
        /// overrides <see cref="BridgeTargetCount"/> if any nodes are specified.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Hives with a large number of pet nodes may generate an excessive amount
        /// of bridge related health checking traffic.  You can mitigate this somewhat 
        /// by designating a smaller number of Swarm target nodes here.
        /// </para>
        /// <para>
        /// It can also be useful for reliability to explicitly identify the target
        /// nodes to ensure that they're running on different underlying hardware
        /// for better reliability.  For example, if a hive had <see cref="BridgeTargetCount "/>
        /// or more nodes running as VMs on the same host or bare metal machines running
        /// in the same rack, it could be possible that all of the target nodes could
        /// be randomly selected to reside on the same host or rack resulting with all
        /// of them being unreachable when the host or rack fails.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "BridgeTargetAddresses", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<IPAddress> BridgeTargetAddresses = new List<IPAddress>();

        /// <summary>
        /// Validates the instance.
        /// </summary>
        /// <param name="context">The validation context.</param>
        public void Validate(TrafficValidationContext context)
        {
            Timeouts              = Timeouts ?? new TrafficTimeouts();
            Resolvers             = Resolvers ?? new List<TrafficResolver>();
            BridgeTargetAddresses = BridgeTargetAddresses ?? new List<IPAddress>();

            if (!Resolvers.Exists(r => r.Name == "docker"))
            {
                Resolvers.Add(
                    new TrafficResolver()
                    {
                        Name = "docker",
                        NameServers = new List<TrafficNameserver>()
                        {
                            new TrafficNameserver()
                            {
                                Name     = "docker0",
                                Endpoint = HiveConst.DockerDnsEndpoint
                            }
                        }
                    });
            }

            if (!NetHelper.IsValidPort(ProxyPorts.PortRange.FirstPort) ||
                !NetHelper.IsValidPort(ProxyPorts.PortRange.LastPort) ||
                ProxyPorts.PortRange.LastPort <= ProxyPorts.PortRange.FirstPort + 1)
            {
                context.Error($"Load balancer port block [{ProxyPorts.PortRange.FirstPort}-{ProxyPorts.PortRange.LastPort}] range is not valid.");
            }

            if (MaxConnections <= 0)
            {
                context.Error($"Load balancer settings [{nameof(MaxConnections)}={MaxConnections}] is not positive.");
            }

            Timeouts.Validate(context);

            if (!Resolvers.Exists(r => r.Name == "docker"))
            {
                context.Error($"Load balancer settings [{nameof(Resolvers)}] must include a [docker] definition.");
            }

            foreach (var resolver in Resolvers)
            {
                resolver.Validate(context);
            }

            if (BridgeTargetCount < 0)
            {
                context.Error($"Load balancer settings [{nameof(BridgeTargetCount)}={BridgeTargetCount}] cannot be negative.");
            }

            if (BridgeTargetCount == 0 && BridgeTargetAddresses.Count == 0)
            {
                context.Error($"Load balancer settings no bridge targets are specified.");
            }
        }
    }
}

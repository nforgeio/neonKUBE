//-----------------------------------------------------------------------------
// FILE:	    LoadBalancerySettings.cs
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

using Neon.Common;
using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes the global settings for a neonCLUSTER load balancer.
    /// </summary>
    public class LoadBalancerSettings
    {
        //---------------------------------------------------------------------
        // Static members

        private const int defaultMaxConnections = 32000;

        /// <summary>
        /// Parses a <see cref="LoadBalancerSettings"/> from a JSON or YAML string,
        /// automatically detecting the input format.
        /// </summary>
        /// <param name="jsonOrYaml">The JSON or YAML input.</param>
        /// <param name="strict">Optionally require that all input properties map to settings properties.</param>
        /// <returns>The parsed <see cref="LoadBalancerSettings"/>.</returns>
        public static LoadBalancerSettings Parse(string jsonOrYaml, bool strict = false)
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
        /// Parses a <see cref="LoadBalancerRule"/> from a JSON string.
        /// </summary>
        /// <param name="jsonText">The input string.</param>
        /// <param name="strict">Optionally require that all input properties map to settings properties.</param>
        /// <returns>The parsed <see cref="LoadBalancerSettings"/>.</returns>
        public static LoadBalancerSettings ParseJson(string jsonText, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(jsonText));

            return NeonHelper.JsonDeserialize<LoadBalancerSettings>(jsonText, strict);
        }

        /// <summary>
        /// Parses a <see cref="LoadBalancerRule"/> from a YAML string.
        /// </summary>
        /// <param name="yamlText">The input string.</param>
        /// <param name="strict">Optionally require that all input properties map to settings properties.</param>
        /// <returns>The parsed <see cref="LoadBalancerSettings"/>.</returns>
        public static LoadBalancerSettings ParseYaml(string yamlText, bool strict = false)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(yamlText));

            return NeonHelper.YamlDeserialize<LoadBalancerSettings>(yamlText, strict);
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// First reserved port on the Docker ingress network in the block allocated to this load balancer.
        /// </summary>
        [JsonProperty(PropertyName = "FirstPort", Required = Required.Always)]
        public int FirstPort { get; set; } = 0;

        /// <summary>
        /// Last reserved port on the Docker ingress network in the block allocated to this load balancer.
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
            get { return FirstPort + 1; }   // $hack(jeff.lill): Hardcodes the convention
        }

        /// <summary>
        /// Returns the first possible TCP port.
        /// </summary>
        [JsonIgnore]
        public int FirstTcpPort
        {
            get { return FirstPort + 2; }   // $hack(jeff.lill): Hardcodes the convention
        }

        /// <summary>
        /// The maximum overall number of simultaneous inbound connections 
        /// to be allowed for the load balancer.  (Defaults to <b>32000</b>).
        /// </summary>
        [JsonProperty(PropertyName = "MaxConnections", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultMaxConnections)]
        public int MaxConnections { get; set; } = defaultMaxConnections;

        /// <summary>
        /// The default endpoint timeouts.
        /// </summary>
        [JsonProperty(PropertyName = "Timeouts", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public LoadBalancerTimeouts Timeouts { get; set; } = new LoadBalancerTimeouts();

        /// <summary>
        /// <para>
        /// The DNS resolvers available for use by the load balancer's rules.
        /// </para>
        /// <note>
        /// This includes the standard <b>docker</b> resolver by default, which 
        /// provides for dynamic service resolution on the attached networks.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "Resolvers")]
        public List<LoadBalancerResolver> Resolvers { get; set; } = new List<LoadBalancerResolver>();

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
        /// load balancer targets.  This can be overridden by explicity designating target
        /// node IP addresses in <see cref="BridgeTargetAddresses"/>.  This
        /// defaults to <b>5</b>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If the number of active workers equals this count then all of them will
        /// be designated as bridge load balancer targets.  If there are more workers, then
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
        /// Clusters with a large number of pet nodes may generate an excessive amount
        /// of bridge related health checking traffic.  You can mitigate this somewhat 
        /// by designating a smaller number of Swarm target nodes here.
        /// </para>
        /// <para>
        /// It can also be useful for reliability to explicitly identify the target
        /// nodes to ensure that they're running on different underlying hardware
        /// for better reliability.  For example, if a cluster had <see cref="BridgeTargetCount "/>
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
        public void Validate(LoadBalancerValidationContext context)
        {
            Timeouts              = Timeouts ?? new LoadBalancerTimeouts();
            Resolvers             = Resolvers ?? new List<LoadBalancerResolver>();
            BridgeTargetAddresses = BridgeTargetAddresses ?? new List<IPAddress>();

            if (!Resolvers.Exists(r => r.Name == "docker"))
            {
                Resolvers.Add(
                    new LoadBalancerResolver()
                    {
                        Name = "docker",
                        NameServers = new List<LoadBalancerNameserver>()
                        {
                            new LoadBalancerNameserver()
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
                context.Error($"Load balancer port block [{FirstPort}-{LastPort}] range is not valid.");
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

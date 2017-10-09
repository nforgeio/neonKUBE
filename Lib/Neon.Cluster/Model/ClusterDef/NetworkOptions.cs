//-----------------------------------------------------------------------------
// FILE:	    NetworkOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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
    /// Describes the network options for a neonCLUSTER.
    /// </summary>
    /// <remarks>
    /// <para>
    /// neonCLUSTERs are provisioned with two standard overlay networks: <b>neon-public</b> and <b>neon-private</b>.
    /// </para>
    /// <para>
    /// <b>neon-public</b> is configured by default on the <b>10.249.0.0/16</b> subnet and is intended to
    /// host public facing service endpoints to be served by the <b>neon-proxy-public</b> proxy service.
    /// </para>
    /// <para>
    /// <b>neon-private</b> is configured by default on the <b>10.248.0.0/16</b> subnet and is intended to
    /// host internal service endpoints to be served by the <b>neon-proxy-private</b> proxy service.
    /// </para>
    /// </remarks>
    public class NetworkOptions
    {
        private const string defaultPublicSubnet  = "10.249.0.0/16";
        private const string defaultPrivateSubnet = "10.248.0.0/16";
        private const string defaultPdnsServerUri = "https://jefflill.github.io/neoncluster/binaries/ubuntu/pdns-server_4.1.0~rc1-1pdns.xenial_amd64.deb";
        private const string defaultPdnsDistUri   = "https://jefflill.github.io/neoncluster/binaries/ubuntu/dnsdist_1.2.0-1pdns.xenial_amd64.deb";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public NetworkOptions()
        {
        }

        /// <summary>
        /// The subnet to be assigned to the built-in <b>neon-public</b> overlay network.  This defaults to <b>10.249.0.0/16</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PublicSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultPublicSubnet)]
        public string PublicSubnet { get; set; } = defaultPublicSubnet;

        /// <summary>
        /// Allow non-Docker swarm mode service containers to attach to the built-in <b>neon-public</b> cluster 
        /// overlay network.  This defaults to <b>true</b> for flexibility but you may consider disabling this for
        /// better security.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The advantage of enabling is is that any container will be able to connect to the default network
        /// and access swarm mode services.  The downside is that this makes it possible for a bad guy who
        /// gains root access to a single node could potentially deploy a malicious container that could also
        /// join the network.  With this disabled, the bad guy would need to gain access to one of the manager
        /// nodes to deploy a malicious service.
        /// </para>
        /// <para>
        /// Unforunately, it's not currently possible to change this setting after a cluster is deployed.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "PublicAttachable", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool PublicAttachable { get; set; } = true;

        /// <summary>
        /// The subnet to be assigned to the built-in <b>neon-public</b> overlay network.  This defaults to <b>10.248.0.0/16</b>.
        /// </summary>
        [JsonProperty(PropertyName = "PrivateSubnet", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(defaultPrivateSubnet)]
        public string PrivateSubnet { get; set; } = defaultPrivateSubnet;

        /// <summary>
        /// Allow non-Docker swarm mode service containers to attach to the built-in <b>neon-private</b> cluster 
        /// overlay network.  This defaults to <b>true</b> for flexibility but you may consider disabling this for
        /// better security.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The advantage of enabling is is that any container will be able to connect to the default network
        /// and access swarm mode services.  The downside is that this makes it possible for a bad guy who
        /// gains root access to a single node could potentially deploy a malicious container that could also
        /// join the network.  With this disabled, the bad guy would need to gain access to one of the manager
        /// nodes to deploy a malicious service.
        /// </para>
        /// <para>
        /// Unforunately, it's not currently possible to change this setting after a cluster is deployed.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "PrivateAttachable", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool PrivateAttachable { get; set; } = true;

        /// <summary>
        /// The IP addresses of the upstream DNS nameservers to be used by the cluster.  This defaults to the 
        /// Google Public DNS servers: <b>[ "8.8.8.8", "8.8.4.4" ]</b> when the property is <c>null</c> or empty.
        /// </summary>
        /// <remarks>
        /// <para>
        /// neonCLUSTERs configure the Consul servers running on the manager nodes to handle the DNS requests
        /// from the cluster host nodes and containers by default.  This enables the registration of services
        /// with Consul that will be resolved to specific IP addresses.  This is used by the <b>proxy-manager</b>
        /// to support stateful services deployed as multiple containers and may also be used in other future
        /// scenarios.
        /// </para>
        /// <para>
        /// neonCLUSTER Consul DNS servers answer requests for names with the <b>cluster</b> top-level domain.
        /// Other requests will be handled recursively by forwarding the request to one of the IP addresses
        /// specified here.
        /// </para>
        /// </remarks>
        [JsonProperty(PropertyName = "Nameservers", Required = Required.Default)]
        [DefaultValue(null)]
        public string[] Nameservers { get; set; } = null;

        /// <summary>
        /// URI for the <a href="https://www.powerdns.com/auth.html">PowerDNS Authoritative Server</a> package 
        /// to use for provisioning cluster DNS services.  This defaults to a known good release.
        /// </summary>
        [JsonProperty(PropertyName = "PdnsServerUri", Required = Required.Default)]
        [DefaultValue(defaultPdnsServerUri)]
        public string PdnsServerUri { get; set; } = defaultPdnsServerUri;

        /// <summary>
        /// URI for the <a href="https://dnsdist.org/">PowerDNS Load Balancer</a> package 
        /// to use for provisioning cluster DNS services.  This defaults to a known good release.
        /// </summary>
        [JsonProperty(PropertyName = "PdnsDistUri", Required = Required.Default)]
        [DefaultValue(defaultPdnsDistUri)]
        public string PdnsDistUri { get; set; } = defaultPdnsDistUri;

        /// <summary>
        /// Validates the options definition and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null);

            if (!NetworkCidr.TryParse(PublicSubnet, out var cidr))
            {
                throw new ClusterDefinitionException($"Invalid [{nameof(PublicSubnet)}={PublicSubnet}].");
            }

            if (!NetworkCidr.TryParse(PrivateSubnet, out cidr))
            {
                throw new ClusterDefinitionException($"Invalid [{nameof(PrivateSubnet)}={PrivateSubnet}].");
            }

            if (PublicSubnet == PrivateSubnet)
            {
                throw new ClusterDefinitionException($"[{nameof(PublicSubnet)}] cannot be the same as [{nameof(PrivateSubnet)}] .");
            }

            if (Nameservers == null || Nameservers.Length == 0)
            {
                Nameservers = new string[] { "8.8.8.8", "8.8.4.4" };
            }

            foreach (var nameserver in Nameservers)
            {
                if (!IPAddress.TryParse(nameserver, out var address))
                {
                    throw new ClusterDefinitionException($"[{nameserver}] is not a valid [{nameof(NetworkOptions)}.{nameof(Nameservers)}] IP address.");
                }
            }

            PdnsServerUri = PdnsServerUri ?? defaultPdnsServerUri;
            
            if (!Uri.TryCreate(PdnsServerUri, UriKind.Absolute, out var uri1))
            {
                throw new ClusterDefinitionException($"[{nameof(PdnsServerUri)}={PdnsServerUri}] is not a valid URI.");
            }

            PdnsDistUri = PdnsDistUri ?? defaultPdnsDistUri;

            if (!Uri.TryCreate(PdnsServerUri, UriKind.Absolute, out var uri2))
            {
                throw new ClusterDefinitionException($"[{nameof(PdnsDistUri)}={PdnsDistUri}] is not a valid URI.");
            }
        }
    }
}

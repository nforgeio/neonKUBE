//-----------------------------------------------------------------------------
// FILE:	    VpnOptions.cs
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
    /// Cluster VPN options.
    /// </summary>
    public class VpnOptions
    {
        /// <summary>
        /// The approximate number of IP addresses from the <see cref="HostingOptions.CloudVpnSubnet"/> to allocate
        /// to the OpenVPN server running on each manager node.  This is currently hardcoded to <b>64</b>.
        /// </summary>
        public const int ServerAddressCount = 64;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public VpnOptions()
        {
        }

        /// <summary>
        /// Enables built-in cluster VPN.  This defaults to <c>true</c>.
        /// </summary>
        [JsonProperty(PropertyName = "Enabled", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// <para>
        /// Specifies whether the same client certificate can be used to establish more than one connection
        /// to the cluster VPN.  This enables a single operator to establish multiple connections (e.g. from
        /// different computers) or for operators to share credentials to simplify certificate management.
        /// This defaults to <c>true</c>.
        /// </para>
        /// <para>
        /// Enabling this trades a bit of security for convienence.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "AllowSharedCredentials", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(true)]
        public bool AllowSharedCredentials { get; set; } = true;

        /// <summary>
        /// Specifies the two-character country code to use for the VPN certificate authority.
        /// This defaults to <b>US</b>.
        /// </summary>
        [JsonProperty(PropertyName = "CertCountryCode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue("US")]
        public string CertCountryCode { get; set; } = "US";

        /// <summary>
        /// Specifies the organization name to use for the VPN certificate authority.  This defaults 
        /// to the cluster name with <b>"-cluster"</b> appended.
        /// </summary>
        [JsonProperty(PropertyName = "CertOrganization", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string CertOrganization { get; set; } = null;

        /// <summary>
        /// Validates the options definition and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        public void Validate(ClusterDefinition clusterDefinition)
        {
            if (!Enabled)
            {
                return;
            }

            if (clusterDefinition.Hosting.Environment == HostingEnvironments.Machine)
            {
                // Ensure that the manager nodes are assigned VPN frontend ports
                // for on-premise deployments.

                foreach (var manager in clusterDefinition.SortedManagers)
                {
                    if (manager.VpnFrontendPort == 0)
                    {
                        manager.VpnFrontendPort = NetworkPorts.OpenVPN;
                    }

                    if (!NetHelper.IsValidPort(manager.VpnFrontendPort))
                    {
                        throw new ClusterDefinitionException($"Manager node [{manager.Name}] assigns [{nameof(NodeDefinition.VpnFrontendPort)}={manager.VpnFrontendPort}] which is not a valid network port.");
                    }
                }
            }

            if (ServerAddressCount <= 0 || ServerAddressCount % 16 != 0)
            {
                throw new ClusterDefinitionException($"VPN [{nameof(ServerAddressCount)}={ServerAddressCount}] is not a positive multiple of 16.");
            }

            if (ServerAddressCount > 65536)
            {
                throw new ClusterDefinitionException($"VPN [{nameof(ServerAddressCount)}={ServerAddressCount}] exceeds the maximum possible value [65536].");
            }

            if (string.IsNullOrEmpty(CertOrganization))
            {
                CertOrganization = $"{clusterDefinition.Name}-cluster";
            }

            if (string.IsNullOrEmpty(CertCountryCode) || CertCountryCode.Length != 2)
            {
                throw new ClusterDefinitionException($"VPN [{nameof(CertCountryCode)}] most be set to a two character country code.");
            }
        }
    }
}

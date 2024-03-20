//-----------------------------------------------------------------------------
// FILE:        LocalHyperVHostingOptions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and

using System;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using Neon.Net;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Specifies hosting settings for the local Microsoft Hyper-V hypervisor.
    /// </summary>
    public class HyperVHostingOptions
    {
        /// <summary>
        /// Returns the subnet to be used for the internal <b>neonkube</b> Hyper-V switch.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public readonly NetworkCidr NeonKubeInternalSubnet;

        /// <summary>
        /// Returns the gateway address for the <see cref="NeonKubeInternalSubnet"/>.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public readonly IPAddress NeonKubeInternalGateway;

        /// <summary>
        /// <b>INTERNAL USE ONLY:</b> Returns the internal IP address reserved for the NEONDESKTOP cluster.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public readonly IPAddress NeonDesktopNodeAddress;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public HyperVHostingOptions()
        {
            NeonKubeInternalSubnet  = NetworkCidr.Parse("100.64.0.0/24");
            NeonKubeInternalGateway = NeonKubeInternalSubnet.FirstUsableAddress;                                        // 100.64.0.1
            NeonDesktopNodeAddress  = NetHelper.AddressIncrement(NeonKubeInternalSubnet.LastAddress, incrementBy: -1);  // 100.64.0.254
        }

        /// <summary>
        /// <para>
        /// Controls whether the cluster will be deployed on the internal <b>neonkube</b> Hyper-V switch
        /// within the <see cref="NeonKubeInternalSubnet"/>.  Note that any <see cref="NetworkOptions.PremiseSubnet"/>
        /// must be already set to <see cref="NeonKubeInternalSubnet"/> and <see cref="NetworkOptions.Gateway"/> must be 
        /// set to <see cref="NeonKubeInternalGateway"/> when this is <c>true</c>.
        /// </para>
        /// <para>
        /// This defaults to <c>false</c>.
        /// </para>
        /// </summary>
        /// <remarks>
        /// <para>
        /// Note that NEONKUBE creates only a single internal Hyper-V switch for the <see cref="HostingEnvironment.HyperV"/>
        /// hosting environment for the <see cref="NeonKubeInternalSubnet"/> (<b>100.64.0.0/24</b>).  Some addresses in
        /// this subnet are reserved:
        /// </para>
        /// <list type="table">
        /// <item>
        ///     <term><b>100.64.0.0</b></term>
        ///     <description>
        ///     Reserved
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>100.64.0.1</b></term>
        ///     <description>
        ///     Gateway for external network access
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>100.64.0.2 ... 253</b></term>
        ///     <description>
        ///     Available for user cluster nodes
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>100.64.0.254</b></term>
        ///     <description>
        ///     Reserved for the NEONDESKTOP single node cluster
        ///     </description>
        /// </item>
        /// <item>
        ///     <term><b>100.64.0.255</b></term>
        ///     <description>
        ///     Reserved for the subnet UDP broadcast address
        ///     </description>
        /// </item>
        /// </list>
        /// </remarks>
        [JsonProperty(PropertyName = "UseInternalSwitch", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "useInternalSwitch", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool UseInternalSwitch { get; set; } = false;

        /// <summary>
        /// <para>
        /// <b>INTERNAL USE ONLY:</b> Indicates whether this is the special NEONDESKTOP built-in
        /// single-node cluster and the node's private address will be overridden by <see cref="NeonDesktopNodeAddress"/>
        /// when this is <c>true</c>.
        /// </para>
        /// <note>
        /// Setting this to <c>true</c> implies setting <see cref="UseInternalSwitch"/><c>=true</c>.
        /// </note>
        /// <para>
        /// This defaults to <c>false</c>.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "NeonDesktopBuiltIn", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "neonDesktopBuiltIn", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool NeonDesktopBuiltIn { get; set; } = false;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            if (NeonDesktopBuiltIn)
            {
                UseInternalSwitch = true;   // NEONDESKTOP clusters always use the internal switch.

                // Ensure that cluster has only one control-plane node and set its
                // address to the reserved IP.

                if (clusterDefinition.NodeDefinitions.Count != 1 || !clusterDefinition.NodeDefinitions.Values.First().IsControlPane)
                {
                    throw new ClusterDefinitionException("The NEONDESKTOP cluster must include only one node and that must be a [control-plane].");
                }

                clusterDefinition.NodeDefinitions.Values.First().Address = NeonDesktopNodeAddress.ToString();
            }

            if (UseInternalSwitch)
            {
                if (!NeonDesktopBuiltIn)
                {
                    // Ensure that no node addresses for a user defined cluster conflict with the
                    // reserved addresses in the internal subnet.

                    var reservedAddresses = new string[]
                    {
                        NeonKubeInternalSubnet.FirstAddress.ToString(),
                        NeonKubeInternalGateway.ToString(),
                        NeonDesktopNodeAddress.ToString(),
                        NeonKubeInternalSubnet.LastAddress.ToString()
                    };

                    foreach (var reservedAddress in reservedAddresses)
                    {
                        foreach (var nodeDefinition in clusterDefinition.SortedNodes)
                        {
                            if (nodeDefinition.Address == reservedAddress)
                            {
                                throw new ClusterDefinitionException($"Node [{nodeDefinition.Name}]'s address [{nodeDefinition.Address}] conflicts with the reserved [{reservedAddress}].");
                            }
                        }
                    }
                }

                // Ensure that the cluster network subnet and gateway options are set to the correct
                // internal switch values.

                if (clusterDefinition.Network.PremiseSubnet != NeonKubeInternalSubnet.ToString())
                {
                    throw new ClusterDefinitionException($"[{nameof(ClusterDefinition.Network)}.{nameof(NetworkOptions.PremiseSubnet)} must be set to [{NeonKubeInternalSubnet}] for the clusters deployed to the internal Hyper-V switch.");
                }

                if (clusterDefinition.Network.Gateway != NeonKubeInternalGateway.ToString())
                {
                    throw new ClusterDefinitionException($"[{nameof(ClusterDefinition.Network)}.{nameof(NetworkOptions.Gateway)} must be set to [{NeonKubeInternalGateway}] for the clusters deployed to the internal Hyper-V switch.");
                }

                if (NeonDesktopBuiltIn)
                {
                    // Ensure that the cluster includes only one node and that it's address 
                    // set to the second to last address in the private subnet.

                    if (clusterDefinition.NodeDefinitions.Count != 1)
                    {
                        throw new ClusterDefinitionException("NEONDESKTOP clusters may only provision a single node.");
                    }

                    if (clusterDefinition.NodeDefinitions.First().Value.Address != NeonDesktopNodeAddress.ToString())
                    {
                        throw new ClusterDefinitionException($"NEONDESKTOP cluster node address must be set to [{NeonKubeInternalSubnet}] (not [{clusterDefinition.NodeDefinitions.First().Value.Address}]).");
                    }
                }
                else
                {
                    // Ensure that no user cluster node definition uses the reserved NEONDESKTOP address.

                    if (clusterDefinition.NodeDefinitions.Values.Any(nodeDefinition => nodeDefinition.Address == NeonDesktopNodeAddress.ToString()))
                    {
                        throw new ClusterDefinitionException($"The [{NeonDesktopNodeAddress}] address may not be assigned to a user cluster; that's reserved for NEONDESKTOP.");
                    }
                }
            }

            clusterDefinition.ValidatePrivateNodeAddresses();   // Private node IP addresses must be assigned and valid.
        }

        /// <summary>
        /// Clears all hosting related secrets.
        /// </summary>
        public void ClearSecrets()
        {
        }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    AddressRule.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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

namespace Neon.Kube
{
    /// <summary>
    /// <para>
    /// Used to whitelist or blacklist an IP address or subnet within a cluster's
    /// <see cref="NetworkOptions.IngressRules"/> or <see cref="NetworkOptions.EgressAddressRules"/>.
    /// </para>
    /// <note>
    /// This is currently supported only for clusters hosted on Azure.  AWS doesn't support
    /// this scenario and we currently don't support automatic router configuration for
    /// on-premise environments.
    /// </note>
    /// </summary>
    public class AddressRule
    {
        //---------------------------------------------------------------------
        // Static members

        private static AddressRule allowAll;
        private static AddressRule denyAll;

        /// <summary>
        /// Returns an <see cref="AddressRule"/> that <b>allows</b> network traffic to/from all IP addresses.
        /// </summary>
        public static AddressRule AllowAll
        {
            get
            {
                if (allowAll == null)
                {
                    allowAll = new AddressRule("any", AddressRuleAction.Allow);
                }

                return allowAll;
            }
        }

        /// <summary>
        /// Returns an <see cref="AddressRule"/> that <b>denies</b> network traffic to/from all IP addresses.
        /// </summary>
        public static AddressRule DenyAll
        {
            get
            {
                if (denyAll == null)
                {
                    denyAll = new AddressRule("any", AddressRuleAction.Deny);
                }

                return denyAll;
            }
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Default constructor.
        /// </summary>
        public AddressRule()
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="addressOrSubnet">
        /// Specifies the IP address or subnet or you may also specify <b>"any"</b>
        /// to specify all possible IP addresses.
        /// </param>
        /// <param name="action">Specifies whether network traffic is to be allowed or denied.</param>
        public AddressRule(string addressOrSubnet, AddressRuleAction action)
        {
            Covenant.Requires<ArgumentException>(!string.IsNullOrEmpty(addressOrSubnet), nameof(addressOrSubnet));
            Covenant.Requires<ArgumentException>(addressOrSubnet.Equals("any", StringComparison.InvariantCultureIgnoreCase) || NetHelper.TryParseIPv4Address(addressOrSubnet, out var v1) || NetworkCidr.TryParse(addressOrSubnet, out var v2), nameof(addressOrSubnet));

            if (addressOrSubnet.Equals("any", StringComparison.InvariantCultureIgnoreCase))
            {
                this.AddressOrSubnet = null;
            }
            else
            {
                this.AddressOrSubnet = addressOrSubnet;
            }

            this.Action = action;
        }

        /// <summary>
        /// Returns <c>true</c> when the all possible IP addresses were specified.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool IsAny => string.IsNullOrEmpty(AddressOrSubnet) || AddressOrSubnet.Equals("any", StringComparison.InvariantCultureIgnoreCase);

        /// <summary>
        /// Returns the specified IP address or subnet or <b>"any"</b> or <c>null</c> for all possible IP addresses.
        /// </summary>
        [JsonProperty(PropertyName = "AddressOrSubnet", Required = Required.AllowNull)]
        [YamlMember(Alias = "addressOrSubnet", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string AddressOrSubnet { get; private set; }

        /// <summary>
        /// Returns the action to performed for network traffic to/from the address or subnet.
        /// </summary>
        [JsonProperty(PropertyName = "Action", Required = Required.Always)]
        [YamlMember(Alias = "action", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public AddressRuleAction Action { get; private set; }

        /// <summary>
        /// Validates the address rule.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <param name="context">Indicates where the address rule is specified, like: <b>ingress-rule-address</b> or <b>egress-address</b></param>
        /// <exception cref="ClusterDefinitionException">Thrown for an invalid rule.</exception>
        public void Validate(ClusterDefinition clusterDefinition, string context)
        {
            if (!IsAny)
            {
                if (!NetHelper.TryParseIPv4Address(AddressOrSubnet, out var v1) && !NetworkCidr.TryParse(AddressOrSubnet, out var v2))
                {
                    throw new ClusterDefinitionException($"Invalid address or subnet [{AddressOrSubnet}] specified for a [{context}].");
                }
            }
        }
    }
}

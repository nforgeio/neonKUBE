//-----------------------------------------------------------------------------
// FILE:	    IngressRule.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;
using Neon.Net;

namespace Neon.Kube
{
    /// <summary>
    /// Specifies a network ingress rule for the cluster.
    /// </summary>
    public class IngressRule
    {
        /// <summary>
        /// The name of the ingress rule.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// Identifies the network protocol.  This defaults to <see cref="IngressProtocol.Tcp"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Protocol", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "protocol", ApplyNamingConventions = false)]
        [DefaultValue(IngressProtocol.Tcp)]
        public IngressProtocol Protocol { get; set; } = IngressProtocol.Tcp;

        /// <summary>
        /// The external ingress port.
        /// </summary>
        [JsonProperty(PropertyName = "ExternalPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "externalPort", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int ExternalPort { get; set; }

        /// <summary>
        /// The Kubernetes NodePort. This is where the ingress gateway is listening.
        /// </summary>
        [JsonProperty(PropertyName = "NodePort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "nodePort", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int NodePort { get; set; }

        /// <summary>
        /// Identifies which group of cluster nodes will receive the network traffic
        /// from this rule.  This defaults to <see cref="IngressRuleTarget.IngressNodes"/>.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        internal IngressRuleTarget Target { get; set; } = IngressRuleTarget.IngressNodes;

        /// <summary>
        /// <para>
        /// Optionally specifies whitelisted and/or blacklisted external addresses for
        /// inbound traffic.  This defaults to allowing inbound traffic from anywhere 
        /// when the property is <c>null</c> or empty.
        /// <note>
        /// Address rules are processed in order, from first to last so you may consider
        /// putting your blacklist rules before your whitelist rules.
        /// </note>
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "AddressRules", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "addressRules", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public List<AddressRule> AddressRules { get; set; } = new List<AddressRule>();

        /// <summary>
        /// <para>
        /// Optionally specifies the TCP idle time out for TCP related ingress protocols like
        /// <see cref="IngressProtocol.Http"/>, <see cref="IngressProtocol.Https"/>, and
        /// <see cref="IngressProtocol.Tcp"/>.  Inbound TCP connections that have no network
        /// traffic going either way will be closed by supported load balancers or routers.
        /// This defaults to <b>4 minutes</b>.
        /// </para>
        /// <note>
        /// <para>
        /// At this point, this property is supported only in cloud environments where we
        /// can easily control the cluster's external loag balancer.  This also has no
        /// impact for non-TCP rules.
        /// </para>
        /// <para>
        /// Also note that this value may be modified to ensure that it honors the range of
        /// values supported by the current cloud.
        /// </para>
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "TcpIdleTimeoutMinutes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "tcpIdleTimeoutMinutes", ApplyNamingConventions = false)]
        [DefaultValue(4)]
        public int TcpIdleTimeoutMinutes { get; set; } = 4;

        /// <summary>
        /// Returns <see cref="TcpIdleTimeoutMinutes"/> as a <see cref="TimeSpan"/>.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        internal TimeSpan TcpIdleTimeout => TimeSpan.FromMinutes(TcpIdleTimeoutMinutes);

        /// <summary>
        /// <para>
        /// Optionally controls whether the cluster router or load balancer sends a TCP RESET
        /// packet to both ends of a TCP connection that has been idle for longer than
        /// <see cref="TcpIdleTimeoutMinutes"/>.  This defaults to <c>false</c>.
        /// </para>
        /// <note>
        /// At this point, this property is supported only in cloud environments where we
        /// can easily control the cluster's external loag balancer.  This also has no
        /// impact for non-TCP rules.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "IdleTcpReset", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "idleTcpReset", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public bool IdleTcpReset { get; set; } = false;

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        [Pure]
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw new ClusterDefinitionException($"[{nameof(IngressRule)}.{nameof(Name)}] is required when specifying an ingress rule.");
            }

            if (!NetHelper.IsValidPort(ExternalPort))
            {
                throw new ClusterDefinitionException($"[{nameof(IngressRule)}.{nameof(ExternalPort)}={ExternalPort}] is not a valid TCP port.");
            }

            if (!NetHelper.IsValidPort(NodePort))
            {
                throw new ClusterDefinitionException($"[{nameof(IngressRule)}.{nameof(NodePort)}={NodePort}] is not a valid TCP port.");
            }

            if (AddressRules != null)
            {
                foreach (var rule in AddressRules)
                {
                    rule.Validate(clusterDefinition, "ingress-rule-address");
                }
            }

            if (TcpIdleTimeoutMinutes <= 0)
            {
                throw new ClusterDefinitionException($"[{nameof(IngressRule)}.{nameof(TcpIdleTimeoutMinutes)}={TcpIdleTimeoutMinutes}] must be greater than 0.");
            }
        }
    }
}

//-----------------------------------------------------------------------------
// FILE:        IngressRule.cs
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

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Specifies a network ingress rule for the cluster.
    /// </summary>
    public class IngressRule
    {
        /// <summary>
        /// The default TCP idle timeout in minutes.  TCP connections managed by a rule
        /// will be reset when the idle timeout is exceeded and <see cref="TcpIdleReset"/>
        /// is set to <c>true</c>.
        /// </summary>
        public const int DefaultTcpIdleTimeoutMinutes = 4;

        /// <summary>
        /// Specifies the name of the ingress rule.
        /// </summary>
        [JsonProperty(PropertyName = "Name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "name", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// Optionally specifies the network protocol.  This defaults to <see cref="IngressProtocol.Tcp"/>.
        /// </summary>
        [JsonProperty(PropertyName = "Protocol", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "protocol", ApplyNamingConventions = false)]
        [DefaultValue(IngressProtocol.Tcp)]
        public IngressProtocol Protocol { get; set; } = IngressProtocol.Tcp;

        /// <summary>
        /// Specifies the external ingress port used to handle external (generally Internet) traffic 
        /// received by the cluster load balancer.
        /// </summary>
        [JsonProperty(PropertyName = "ExternalPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "externalPort", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int ExternalPort { get; set; }

        /// <summary>
        /// Specifies the port on cluster nodes where external traffic received by the load balancer 
        /// on <see cref="ExternalPort"/> will be forwarded.  The cluster's ingress gateway
        /// (Istio) will be configured to listen for traffic on this port and route it into
        /// the cluster.
        /// </summary>
        [JsonProperty(PropertyName = "NodePort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "nodePort", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int NodePort { get; set; }

        /// <summary>
        /// <para>
        /// Specifies the target ingress port internal to the cluster.  The cluster's ingress gateway
        /// (Istio) applies routing rules (virtual service) to the network traffic as it was
        /// received on <see cref="TargetPort"/>.  This decouples routing rules from <see cref="NodePort"/>
        /// which may change for different hosting environments.
        /// </para>
        /// <para>
        /// This property is optional and defaults to zero, indicating that the traffic should
        /// be routed to just the node port but <b>should not be routed through ingress gateway</b>.
        /// This is useful for handling UDP traffic which Istio doesn't currently support and
        /// perhaps some other scenarios.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "TargetPort", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "targetPort", ApplyNamingConventions = false)]
        [DefaultValue(0)]
        public int TargetPort { get; set; } = 0;

        /// <summary>
        /// Identifies which group of cluster nodes will receive the network traffic
        /// from this rule.  This defaults to <see cref="IngressRuleTarget.Ingress"/>.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        internal IngressRuleTarget Target { get; set; } = IngressRuleTarget.Ingress;

        /// <summary>
        /// <para>
        /// Optionally specifies the default cluster load balancer health check settings
        /// for the rule.  This overrides the default <see cref="NetworkOptions.IngressHealthCheck"/>
        /// settings.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "IngressHealthCheck", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "ingressHealthCheck", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public HealthCheckOptions IngressHealthCheck { get; set; } = null;

        /// <summary>
        /// <para>
        /// Optionally specifies whitelisted and/or blacklisted external addresses for
        /// inbound traffic.  This defaults to allowing inbound traffic from anywhere 
        /// when the property is <c>null</c> or empty.
        /// </para>
        /// <note>
        /// Address rules are processed in order, from first to last so you may consider
        /// putting your blacklist rules before your whitelist rules.
        /// </note>
        /// <note>
        /// This is currently supported only for clusters hosted on Azure.  AWS doesn't support
        /// this scenario and we currently don't support automatic router configuration for
        /// on-premise environments.
        /// </note>
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
        /// At this point, this property is supported only in cloud environments where we
        /// can easily control the cluster's external loag balancer.  This also has no
        /// impact for <see cref="IngressProtocol.Udp"/> protocol.
        /// </note>
        /// <note>
        /// Cluster setup may modify this value to ensure that it honors the range of
        /// values supported by the target cloud cloud.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "TcpIdleTimeoutMinutes", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "tcpIdleTimeoutMinutes", ApplyNamingConventions = false)]
        [DefaultValue(DefaultTcpIdleTimeoutMinutes)]
        public int TcpIdleTimeoutMinutes { get; set; } = DefaultTcpIdleTimeoutMinutes;

        /// <summary>
        /// Returns <see cref="TcpIdleTimeoutMinutes"/> as a <see cref="TimeSpan"/>.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        internal TimeSpan TcpIdleTimeout => TimeSpan.FromMinutes(TcpIdleTimeoutMinutes);

        /// <summary>
        /// <para>
        /// Optionally specifies whether the cluster router or load balancer sends a TCP RESET
        /// packet to both ends of a TCP connection that has been idle for longer than
        /// <see cref="TcpIdleTimeoutMinutes"/>.  This defaults to <c>true</c>.
        /// </para>
        /// <note>
        /// At this point, this property is supported only in cloud environments where we
        /// can easily control the cluster's external loag balancer.  This also has no
        /// impact for non-TCP rules.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "TcpIdleReset", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "tcpIdleReset", ApplyNamingConventions = false)]
        [DefaultValue(true)]
        public bool TcpIdleReset { get; set; } = true;

        /// <summary>
        /// Validates the options.
        /// </summary>
        /// <param name="clusterDefinition">Specifies the cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        internal void Validate(ClusterDefinition clusterDefinition)
        {
            if (string.IsNullOrEmpty(Name))
            {
                throw new ClusterDefinitionException($"[{nameof(IngressRule)}.{nameof(Name)}] is required when specifying an ingress rule.");
            }

            if (!NetHelper.IsValidPort(ExternalPort))
            {
                throw new ClusterDefinitionException($"Rule [{Name}]: [{nameof(IngressRule)}.{nameof(ExternalPort)}={ExternalPort}] is not a valid TCP port.");
            }

            if (!NetHelper.IsValidPort(NodePort))
            {
                throw new ClusterDefinitionException($"Rule [{Name}]: [{nameof(IngressRule)}.{nameof(NodePort)}={NodePort}] is not a valid TCP port.");
            }

            if (!NetHelper.IsValidPort(TargetPort) && TargetPort != 0)  // NOTE: [TargetPort=0] indicates that the traffic is not managed by the ingress gateway.
            {
                throw new ClusterDefinitionException($"Rule [{Name}]: [{nameof(IngressRule)}.{nameof(TargetPort)}={TargetPort}] is not a valid TCP port.");
            }

            if (TargetPort > 0 && Protocol == IngressProtocol.Udp)
            {
                throw new ClusterDefinitionException($"Rule [{Name}]: [{nameof(IngressRule)}.{nameof(TargetPort)}={TargetPort}] implies that traffic will be processed by the Istio gateway which does not support UDP traffic.");
            }

            if (AddressRules != null)
            {
                foreach (var rule in AddressRules)
                {
                    rule.Validate(clusterDefinition, Name);
                }
            }

            if (TcpIdleTimeoutMinutes <= 0)
            {
                throw new ClusterDefinitionException($"Rule [{Name}]: [{nameof(IngressRule)}.{nameof(TcpIdleTimeoutMinutes)}={TcpIdleTimeoutMinutes}] must be greater than 0.");
            }

            IngressHealthCheck?.Validate(clusterDefinition, $"Rule [{Name}]: [{nameof(NetworkOptions)}.{nameof(NetworkOptions.IngressRules)}]");
        }
    }
}

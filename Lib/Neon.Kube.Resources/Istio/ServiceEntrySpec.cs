//-----------------------------------------------------------------------------
// FILE:	    ServiceEntrySpec.cs
// CONTRIBUTOR: Marcus Bowyer
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

using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;

namespace Neon.Kube.Resources
{
    /// <summary>
    /// ServiceEntry enables adding additional entries into Istio’s internal service registry.
    /// </summary>
    public class ServiceEntrySpec
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public ServiceEntrySpec()
        {
        }

        /// <summary>
        /// The destination hosts.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The hosts associated with the ServiceEntry. Could be a DNS name with wildcard prefix.
        /// </para>
        /// <para>
        /// 1. The hosts field is used to select matching hosts in VirtualServices and DestinationRules.
        /// 2. For HTTP traffic the HTTP Host/Authority header will be matched against the hosts field.
        /// 3. For HTTPs or TLS traffic containing Server Name Indication(SNI), the SNI value will be matched against the hosts field.
        /// </para>
        /// <note>
        /// When resolution is set to type DNS and no endpoints are specified, the host field will be used as the DNS name of the endpoint to route traffic to.
        /// </note>
        /// <note>
        /// If the hostname matches with the name of a service from another service registry such as Kubernetes that also supplies its own set of endpoints, 
        /// the ServiceEntry will be treated as a decorator of the existing Kubernetes service. Properties in the service entry will be added to the Kubernetes 
        /// service if applicable. Currently, only the following additional properties will be considered by istiod:
        /// 1. subjectAltNames: In addition to verifying the SANs of the service accounts associated with the pods of the service, the SANs specified here will 
        /// also be verified.
        /// </note>
        /// </remarks>
        [JsonProperty(PropertyName = "hosts", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> Hosts { get; set; }

        /// <summary>
        /// The virtual IP addresses associated with the service. Could be CIDR prefix. For HTTP traffic, generated route configurations will include http route 
        /// domains for both the addresses and hosts field values and the destination will be identified based on the HTTP Host/Authority header. If one or more IP 
        /// addresses are specified, the incoming traffic will be identified as belonging to this service if the destination IP matches the IP/CIDRs specified in the 
        /// addresses field. If the Addresses field is empty, traffic will be identified solely based on the destination port. In such scenarios, the port on which 
        /// the service is being accessed must not be shared by any other service in the mesh. In other words, the sidecar will behave as a simple TCP proxy, 
        /// forwarding incoming traffic on a specified port to the specified destination endpoint IP/host. Unix domain socket addresses are not supported in this 
        /// field.
        /// </summary>
        [JsonProperty(PropertyName = "addresses", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> Addresses { get; set; }

        /// <summary>
        /// The ports associated with the external service. If the Endpoints are Unix domain socket addresses, there must be exactly one port.
        /// </summary>
        [JsonProperty(PropertyName = "ports", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<Port> Ports { get; set; }

        /// <summary>
        /// Specify whether the service should be considered external to the mesh or part of the mesh.
        /// </summary>
        [JsonProperty(PropertyName = "location", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Location? Location { get; set; }

        /// <summary>
        /// Service discovery mode for the hosts. Care must be taken when setting the resolution mode to NONE for a TCP port without accompanying 
        /// IP addresses. In such cases, traffic to any IP on said port will be allowed (i.e. 0.0.0.0:PORT).
        /// </summary>
        [JsonProperty(PropertyName = "resolution", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Resolution Resolution { get; set; }

        /// <summary>
        /// <para>
        /// One or more endpoints associated with the service. Only one of endpoints or workloadSelector can be specified.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "endpoints", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<WorkloadEntry> Endpoints { get; set; }

        /// <summary>
        /// <para>
        /// Applicable only for MESH_INTERNAL services. Only one of endpoints or workloadSelector can be specified. Selects one or more Kubernetes
        /// pods or VM workloads (specified using WorkloadEntry) based on their labels. The WorkloadEntry object representing the VMs should be
        /// defined in the same namespace as the ServiceEntry.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "workloadSelector", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public WorkloadSelector WorkloadSelector { get; set; }

        /// <summary>
        /// <para>
        /// A list of namespaces to which this service is exported. Exporting a service allows it to be used by sidecars, gateways and virtual services 
        /// defined in other namespaces. This feature provides a mechanism for service owners and mesh administrators to control the visibility of 
        /// services across namespace boundaries.
        /// </para>
        /// <para>
        /// If no namespaces are specified then the service is exported to all namespaces by default.
        /// </para>
        /// <para>
        /// The value “.” is reserved and defines an export to the same namespace that the service is declared in. Similarly the value “*” is reserved 
        /// and defines an export to all namespaces.
        /// </para>
        /// <para>
        /// For a Kubernetes Service, the equivalent effect can be achieved by setting the annotation “networking.istio.io/exportTo” to a comma-separated
        /// list of namespace names.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "exportTo", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> ExportTo { get; set; }

        /// <summary>
        /// <para>
        /// If specified, the proxy will verify that the server certificate’s subject alternate name matches one of the specified values.
        /// </para>
        /// <note>
        /// When using the workloadEntry with workloadSelectors, the service account specified in the workloadEntry will also be used to derive 
        /// the additional subject alternate names that should be verified.
        /// </note>
        /// </summary>
        [JsonProperty(PropertyName = "subjectAltNames", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> SubjectAltNames { get; set; }
    }
}

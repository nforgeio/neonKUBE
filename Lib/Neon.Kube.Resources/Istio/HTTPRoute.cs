//-----------------------------------------------------------------------------
// FILE:	    HTTPRoute.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// Describes the properties of a specific HTTPRoute of a service.
    /// </summary>
    public class HTTPRoute : IValidate
    {
        /// <summary>
        /// Initializes a new instance of the HTTPRoute class.
        /// </summary>
        public HTTPRoute()
        {
        }

        /// <summary>
        /// The name assigned to the route for debugging purposes. The route’s name will be concatenated with the match’s name and will be 
        /// logged in the access logs for requests matching this route/match.
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// <para>
        /// Match conditions to be satisfied for the rule to be activated. All conditions inside a single match block have AND semantics, 
        /// while the list of match blocks have OR semantics. The rule is matched if any one of the match blocks succeed.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "match", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<HTTPMatchRequest> Match { get; set; }

        /// <summary>
        /// <para>
        /// A HTTP rule can either redirect or forward (default) traffic. The forwarding target can be one of several versions of a service (see glossary 
        /// in beginning of document). Weights associated with the service version determine the proportion of traffic it receives.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "route", Required = Required.Always)]
        public List<HTTPRouteDestination> Route { get; set; }

        /// <summary>
        /// <para>
        /// A HTTP rule can either redirect or forward (default) traffic. If traffic passthrough option is specified in the rule, route/redirect will 
        /// be ignored. The redirect primitive can be used to send a HTTP 301 redirect to a different URI or Authority.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "redirect", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public HTTPRedirect Redirect { get; set; }

        /// <summary>
        /// <para>
        /// Delegate is used to specify the particular VirtualService which can be used to define delegate HTTPRoute.
        /// </para>
        /// <para>
        /// It can be set only when Route and Redirect are empty, and the route rules of the delegate VirtualService will be merged with that in the current one.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Only one level delegation is supported.
        /// The delegate’s HTTPMatchRequest must be a strict subset of the root’s, otherwise there is a conflict and the HTTPRoute will not take effect.
        /// </remarks>
        [JsonProperty(PropertyName = "delegate", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Delegate Delegate { get; set; }

        /// <summary>
        /// <para>
        /// Rewrite HTTP URIs and Authority headers. Rewrite cannot be used with Redirect primitive. Rewrite will be performed before forwarding.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "rewrite", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public HTTPRewrite Rewrite { get; set; }

        /// <summary>
        /// Timeout for HTTP requests, default is disabled.
        /// </summary>
        [JsonProperty(PropertyName = "timeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Timeout { get; set; }

        /// <summary>
        /// Retry policy for HTTP requests.
        /// </summary>
        [JsonProperty(PropertyName = "retries", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public HTTPRetry Retries { get; set; }

        /// <summary>
        /// <para>
        /// Fault injection policy to apply on HTTP traffic at the client side. Note that timeouts or retries will not be enabled when faults 
        /// are enabled on the client side.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "fault", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public HTTPFaultInjection Fault { get; set; }

        /// <summary>
        /// <para>
        /// Mirror HTTP traffic to a another destination in addition to forwarding the requests to the intended destination. Mirrored traffic
        /// is on a best effort basis where the sidecar/gateway will not wait for the mirrored cluster to respond before returning the response
        /// from the original destination. Statistics will be generated for the mirrored destination.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "mirror", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Destination Mirror { get; set; }

        /// <summary>
        /// <para>
        /// Percentage of the traffic to be mirrored by the mirror field. If this field is absent, all the traffic (100%) will be mirrored. 
        /// </para>
        /// </summary>
        /// <remarks>Max value is 100.</remarks>
        [JsonProperty(PropertyName = "mirrorPercentage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Percent MirrorPercentage { get; set; }

        /// <summary>
        /// <para>
        /// Cross-Origin Resource Sharing policy (CORS). Refer to CORS for further details about cross origin resource sharing.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "corsPolicy", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public CorsPolicy CorsPolicy { get; set; }

        /// <summary>
        /// Header manipulation rules
        /// </summary>
        [JsonProperty(PropertyName = "headers", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Headers Headers { get; set; }

        /// <summary>
        /// <para>
        /// Percentage of the traffic to be mirrored by the mirror field. Use of integer mirror_percent value is deprecated. Use the double 
        /// mirror_percentage field instead
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "mirrorPercent", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public UInt32Value MirrorPercent { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
        }
    }
}

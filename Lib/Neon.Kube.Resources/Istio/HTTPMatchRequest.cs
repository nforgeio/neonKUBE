//-----------------------------------------------------------------------------
// FILE:        HTTPMatchRequest.cs
// CONTRIBUTOR: Marcus Bowyer
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
    /// HttpMatchRequest specifies a set of criterion to be met in order for the rule to be applied to the HTTP request. For example, the following 
    /// restricts the rule to match only requests where the URL path starts with /ratings/v2/ and the request contains a custom end-user header 
    /// with value jason.
    /// </summary>
    public class HTTPMatchRequest : IValidate
    {
        /// <summary>
        /// Initializes a new instance of the HTTPMatchRequest class.
        /// </summary>
        public HTTPMatchRequest()
        {
        }

        /// <summary>
        /// The name assigned to a match. The match’s name will be concatenated with the parent route’s name and will be logged in the 
        /// access logs for requests matching this route.
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Name { get; set; }

        /// <summary>
        /// <para>
        /// URI to match values are case-sensitive and formatted as follows:
        ///
        /// exact: "value" for exact string match
        /// 
        /// prefix: "value" for prefix-based match
        ///
        /// regex: "value" for RE2 style regex-based match(https://github.com/google/re2/wiki/Syntax).
        /// </para>
        /// </summary>
        /// <remarks>
        /// Note: Case-insensitive matching could be enabled via the ignore_uri_case flag.
        /// </remarks>
        [JsonProperty(PropertyName = "uri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public StringMatch Uri { get; set; }

        /// <summary>
        /// <para>
        /// URI Scheme values are case-sensitive and formatted as follows:
        ///
        /// exact: "value" for exact string match
        /// 
        /// prefix: "value" for prefix-based match
        ///
        /// regex: "value" for RE2 style regex-based match(https://github.com/google/re2/wiki/Syntax).
        /// </para>
        /// </summary>
        /// <remarks>
        /// Note: Case-insensitive matching could be enabled via the ignore_uri_case flag.
        /// </remarks>
        [JsonProperty(PropertyName = "scheme", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public StringMatch Scheme { get; set; }

        /// <summary>
        /// <para>
        /// HTTP Method values are case-sensitive and formatted as follows:
        ///
        /// exact: "value" for exact string match
        /// 
        /// prefix: "value" for prefix-based match
        ///
        /// regex: "value" for RE2 style regex-based match(https://github.com/google/re2/wiki/Syntax).
        /// </para>
        /// </summary>
        /// <remarks>
        /// Note: Case-insensitive matching could be enabled via the ignore_uri_case flag.
        /// </remarks>
        [JsonProperty(PropertyName = "method", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public StringMatch Method { get; set; }

        /// <summary>
        /// <para>
        /// URI Authority values are case-sensitive and formatted as follows:
        ///
        /// exact: "value" for exact string match
        /// 
        /// prefix: "value" for prefix-based match
        ///
        /// regex: "value" for RE2 style regex-based match(https://github.com/google/re2/wiki/Syntax).
        /// </para>
        /// </summary>
        /// <remarks>
        /// Note: Case-insensitive matching could be enabled via the ignore_uri_case flag.
        /// </remarks>
        [JsonProperty(PropertyName = "authority", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public StringMatch Authority { get; set; }

        /// <summary>
        /// <para>
        /// The header keys must be lowercase and use hyphen as the separator, e.g. x-request-id.
        ///
        /// exact: "value" for exact string match
        /// 
        /// prefix: "value" for prefix-based match
        ///
        /// regex: "value" for RE2 style regex-based match(https://github.com/google/re2/wiki/Syntax).
        /// 
        /// If the value is empty and only the name of header is specfied, presence of the header is checked. Note: The keys uri, scheme,
        /// method, and authority will be ignored.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Note: Case-insensitive matching could be enabled via the ignore_uri_case flag.
        /// </remarks>
        [JsonProperty(PropertyName = "headers", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, StringMatch> Headers { get; set; }

        /// <summary>
        /// <para>
        /// Specifies the ports on the host that is being addressed. Many services only expose a single port or label ports with the protocols
        /// they support, in these cases it is not required to explicitly select the port.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "port", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int? Port { get; set; }

        /// <summary>
        /// <para>
        /// One or more labels that constrain the applicability of a rule to source (client) workloads with the given labels. If the V1VirtualService 
        /// has a list of gateways specified in the top-level gateways field, it must include the reserved gateway mesh for this field to be applicable.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "sourceLabels", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, string> SourceLabels { get; set; }

        /// <summary>
        /// <para>
        /// Names of gateways where the rule should be applied. Gateway names in the top-level gateways field of the V1VirtualService (if any) are
        /// overridden. The gateway match is independent of sourceLabels.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "gateways", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> Gateways { get; set; }

        /// <summary>
        /// <para>
        /// Query parameters for matching.
        ///
        /// Ex:
        /// 
        /// - For a query parameter like “?key=true”, the map key would be “key” and the string match could be defined as exact: "true".
        /// 
        /// - For a query parameter like “?key”, the map key would be “key” and the string match could be defined as exact: "".
        /// 
        /// - For a query parameter like “?key=123”, the map key would be “key” and the string match could be defined as regex: "\d+$". 
        /// Note that this configuration will only match values like “123” but not “a123” or “123a”.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Note: prefix matching is currently not supported.
        /// </remarks>
        [JsonProperty(PropertyName = "queryParams", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, StringMatch> QueryParameters { get; set; }

        /// <summary>
        /// <para>
        /// Flag to specify whether the URI matching should be case-insensitive.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Note: The case will be ignored only in the case of exact and prefix URI matches.
        /// </remarks>
        [JsonProperty(PropertyName = "ignoreUriCase", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public bool? IgnoreUriCase { get; set; }

        /// <summary>
        /// <para>
        /// withoutHeader has the same syntax with the header, but has opposite meaning. If a header is matched with a matching rule among 
        /// withoutHeader, the traffic becomes not matched one.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Note: Case-insensitive matching could be enabled via the ignore_uri_case flag.
        /// </remarks>
        [JsonProperty(PropertyName = "withoutHeaders", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, StringMatch> WithoutHeaders { get; set; }

        /// <summary>
        /// <para>
        /// Source namespace constraining the applicability of a rule to workloads in that namespace. If the V1VirtualService has a list of gateways 
        /// specified in the top-level gateways field, it must include the reserved gateway mesh for this field to be applicable.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Note: Case-insensitive matching could be enabled via the ignore_uri_case flag.
        /// </remarks>
        [JsonProperty(PropertyName = "sourceNamespace", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string SourceNamespace { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
        }
    }
}

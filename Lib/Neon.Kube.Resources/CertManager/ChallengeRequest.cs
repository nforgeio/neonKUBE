//-----------------------------------------------------------------------------
// FILE:	    ChallengeRequest.cs
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
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;

namespace Neon.Kube.Resources.CertManager
{
    /// <summary>
    /// 
    /// </summary>
    public class ChallengeRequest
    {
        /// <summary>
        /// <para>
        /// UID is an identifier for the individual request/response. It allows us to distinguish instances of requests which are
        /// otherwise identical (parallel requests, requests when earlier requests did not modify etc)
        /// </para>
        /// <para>
        /// The UID is meant to track the round trip (request/response) between the KAS and the WebHook, not the user request.
        /// It is suitable for correlating log entries between the webhook and apiserver, for either auditing or debugging.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "uid", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Uid { get; set; }

        /// <summary>
        /// <para>
        /// Action is one of 'present' or 'cleanup'.
        /// </para>
        /// <para>
        /// If the action is 'present', the record will be presented with the
        /// solving service.
        /// </para>
        /// <para>
        /// If the action is 'cleanup', the record will be cleaned up with the
        /// solving service.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "action", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
        public ChallengeAction Action { get; set; }

        /// <summary>
        /// Type is the type of ACME challenge. Only dns-01 is currently supported.
        /// </summary>
        [JsonProperty(PropertyName = "type", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Type { get; set; }

        /// <summary>
        /// <para>
        /// DNSName is the name of the domain that is actually being validated, as
        /// requested by the user on the Certificate resource.
        /// </para>
        /// <para>
        /// This will be of the form 'example.com' from normal hostnames, and
        /// '*.example.com' for wildcards.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "dnsName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string DnsName { get; set; }

        /// <summary>
        /// <para>
        /// Key is the key that should be presented.
        /// </para>
        /// <para>
        /// This key will already be signed by the account that owns the challenge.
        /// For DNS01, this is the key that should be set for the TXT record for
        /// ResolveFQDN.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "key", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Key { get; set; }

        /// <summary>
        /// <para>
        /// ResourceNamespace is the namespace containing resources that are
        /// referenced in the providers config.
        /// </para>
        /// <para>
        /// If this request is solving for an Issuer resource, this will be the
        /// namespace of the Issuer.
        /// </para>
        /// <para>
        /// If this request is solving for a ClusterIssuer resource, this will be
        /// the configured 'cluster resource namespace'
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "resourceNamespace", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ResourceNamespace { get; set; }

        /// <summary>
        /// <para>
        /// ResolvedFQDN is the fully-qualified domain name that should be
        /// updated/presented after resolving all CNAMEs.
        /// </para>
        /// <para>
        /// This should be honoured when using the DNS01 solver type.
        /// </para>
        /// <para>
        /// This will be of the form '_acme-challenge.example.com.'.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "resolvedFQDN", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ResolvedFQDN { get; set; }

        /// <summary>
        /// <para>
        /// ResolvedZone is the zone encompassing the ResolvedFQDN.
        /// This is included as part of the ChallengeRequest so that webhook
        /// implementers do not need to implement their own SOA recursion logic.
        /// </para>
        /// <para>
        /// This indicates the zone that the provided FQDN is encompassed within,
        /// determined by performing SOA record queries for each part of the FQDN
        /// until an authoritative zone is found.
        /// </para>
        /// <para>
        /// This will be of the form 'example.com.'.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "resolvedZone", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ResolvedZone { get; set; }

        /// <summary>
        /// <para>
        /// AllowAmbientCredentials advises webhook implementations that they can
        /// use 'ambient credentials' for authenticating with their respective
        /// DNS provider services.
        /// </para>
        /// <para>
        /// This field SHOULD be honoured by all DNS webhook implementations, but
        /// in certain instances where it does not make sense to honour this option,
        /// an implementation may ignore it.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "allowAmbientCredentials", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public bool AllowAmbientCredentials { get; set; }

        /// <summary>
        /// <para>
        /// Config contains unstructured JSON configuration data that the webhook
        /// implementation can unmarshal in order to fetch secrets or configure
        /// connection details etc.
        /// </para>
        /// <para>
        /// Secret values should not be passed in this field, in favour of
        /// references to Kubernetes Secret resources that the webhook can fetch.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "config", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Dictionary<string, object> Config { get; set; }
    }
}

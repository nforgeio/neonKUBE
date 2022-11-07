//-----------------------------------------------------------------------------
// FILE:	    CertificateSpec.cs
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;
using Neon.JsonConverters;
using Newtonsoft.Json;

namespace Neon.Kube
{
    /// <summary>
    /// The kubernetes spec for a cert-manager certificate.
    /// </summary>
    public class CertificateSpec
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public CertificateSpec()
        {
        }

        /// <summary>
        /// CommonName is a common name to be used on the Certificate. The CommonName should have a length of 64 characters or fewer to avoid 
        /// generating invalid CSRs. This value is ignored by TLS clients when any subject alt name is set. This is x509 
        /// behaviour: https://tools.ietf.org/html/rfc6125#section-6.4.4'
        /// </summary>
        [JsonProperty(PropertyName = "commonName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string CommonName { get; set; }

        /// <summary>
        /// A list of DNS subjectAltNames to be set on the Certificate.
        /// </summary>
        [JsonProperty(PropertyName = "dnsNames", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> DnsNames { get; set; }

        /// <summary>
        /// The requested 'duration' (i.e. lifetime) of the Certificate. This option may be ignored/overridden by some issuer types. If unset 
        /// this defaults to 90 days. Certificate will be renewed either 2/3 through its duration or `renewBefore` period before its expiry, 
        /// whichever is later. Minimum accepted duration is 1 hour. Value must be in units accepted by GOLANG <b>time.ParseDuration()</b>:
        /// https://golang.org/pkg/time/#ParseDuration
        /// </summary>
        [JsonProperty(PropertyName = "duration", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Duration { get; set; }

        /// <summary>
        /// A list of email <b>subjectAltNames</b> to be set on the Certificate.
        /// </summary>
        [JsonProperty(PropertyName = "emailAddresses", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> EmailAddresses { get; set; }

        /// <summary>
        /// Controls whether key usages should be present in the CertificateRequest.
        /// </summary>
        [JsonProperty(PropertyName = "encodeUsagesInRequest", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public bool? EncodeUsagesInRequest { get; set; }

        /// <summary>
        /// A list of IP address <b>subjectAltNames</b> to be set on the Certificate.
        /// </summary>
        [JsonProperty(PropertyName = "ipAddresses", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> IpAddresses { get; set; }

        /// <summary>
        /// Whether this Certificate as valid for certificate signing. This will automatically add the `cert sign` usage to the list of `usages`.
        /// </summary>
        [JsonProperty(PropertyName = "isCA", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool? IsCA { get; set; }

        /// <summary>
        /// Configures additional keystore output formats stored in the `secretName` Secret resource.
        /// </summary>
        [JsonProperty(PropertyName = "keystores", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Keystores Keystores { get; set; }

        /// <summary>
        /// A reference to the issuer for this certificate.
        /// </summary>
        [JsonProperty(PropertyName = "issuerRef", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public IssuerRef IssuerRef { get; set; }

        /// <summary>
        /// The key bit size of the corresponding private key for this certificate. If `keyAlgorithm` is set to `rsa`, valid values are `2048`, 
        /// `4096` or `8192`, and will default to `2048` if not specified. If `keyAlgorithm` is set to `ecdsa`, valid values are `256`, `384` 
        /// or `521`, and will default to `256` if not specified. No other values are allowed.
        /// </summary>
        [JsonProperty(PropertyName = "keySize", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(256)]
        public int? KeySize { get; set; }

        /// <summary>
        /// A list of organizations to be used on the Certificate.
        /// </summary>
        [JsonProperty(PropertyName = "organization", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> Organization { get; set; }

        /// <summary>
        /// Options to control private keys used for the Certificate.
        /// </summary>
        [JsonProperty(PropertyName = "privateKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public PrivateKey PrivateKey { get; set; }

        /// <summary>
        /// How long before the currently issued certificate's expiry cert-manager should renew the certificate. The default is 2/3 of the issued 
        /// certificate's duration. Minimum accepted value is 5 minutes. Value must be in units accepted by 
        /// Go time.ParseDuration https://golang.org/pkg/time/#ParseDuration
        /// </summary>
        [JsonProperty(PropertyName = "renewBefore", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string RenewBefore { get; set; }

        /// <summary>
        /// RevisionHistoryLimit is the maximum number of CertificateRequest revisions that are maintained in the Certificate's history. Each 
        /// revision represents a single `CertificateRequest` created by this Certificate, either when it was created, renewed, or Spec was changed. 
        /// Revisions will be removed by oldest first if the number of revisions exceeds this number. If set, revisionHistoryLimit must be a value 
        /// of `1` or greater. If unset (`nil`), revisions will not be garbage collected. Default value is `nil`.
        /// </summary>
        [JsonProperty(PropertyName = "revisionHistoryLimit", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int? RevisionHistoryLimit { get; set; }

        /// <summary>
        /// The name of the secret resource that will be automatically created and managed by this Certificate resource. It will be populated 
        /// with a private key and certificate, signed by the denoted issuer.
        /// </summary>
        [JsonProperty(PropertyName = "secretName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string SecretName { get; set; }

        /// <summary>
        /// A list of URI subjectAltNames to be set on the Certificate.
        /// </summary>
        [JsonProperty(PropertyName = "uris", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> Uris { get; set; }

        /// <summary>
        /// Full X509 name specification (https://golang.org/pkg/crypto/x509/pkix/#Name).
        /// </summary>
        [JsonProperty(PropertyName = "subject", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Subject Subject { get; set; }

        /// <summary>
        /// Usages is the set of x509 usages that are requested for the certificate.
        /// </summary>
        [JsonProperty(PropertyName = "usages", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        [System.Text.Json.Serialization.JsonConverter(typeof(JsonGenericConverter<X509Usages[]>))]
        public X509Usages[] Usages { get; set; }
    }
}

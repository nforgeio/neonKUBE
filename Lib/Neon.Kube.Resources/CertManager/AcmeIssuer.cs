//-----------------------------------------------------------------------------
// FILE:	    AcmeIssuer.cs
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
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;

using Neon.Common;
using Neon.JsonConverters;

using Newtonsoft.Json;

using YamlDotNet.Serialization;

namespace Neon.Kube.Resources.CertManager
{
    /// <summary>
    /// The kubernetes spec for a cert-manager ClusterIssuer.
    /// </summary>
    public class AcmeIssuer
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public AcmeIssuer()
        {
        }

        /// <summary>
        /// Email is the email address to be associated with the ACME account. This field is optional, but it is strongly recommended to 
        /// be set. It will be used to contact you in case of issues with your account or certificates, including expiry notification emails. 
        /// This field may be updated after the account is initially registered.
        /// </summary>
        [JsonProperty(PropertyName = "email", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "email", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Email { get; set; } = null;

        /// <summary>
        /// Server is the URL used to access the ACME server’s ‘directory’ endpoint. For example, for Let’s Encrypt’s staging endpoint, 
        /// you would use: “https://acme-staging-v02.api.letsencrypt.org/directory”. Only ACME v2 endpoints (i.e. RFC 8555) are supported.
        /// </summary>
        [JsonProperty(PropertyName = "server", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "server", ApplyNamingConventions = false)]
        [DefaultValue("https://acme-v02.api.letsencrypt.org/directory")]
        public string Server { get; set; } = "https://acme-v02.api.letsencrypt.org/directory";

        /// <summary>
        /// PreferredChain is the chain to use if the ACME server outputs multiple. PreferredChain is no guarantee that this one gets 
        /// delivered by the ACME endpoint. For example, for Let’s Encrypt’s DST crosssign you would use: “DST Root CA X3” or “ISRG Root X1”
        /// for the newer Let’s Encrypt root CA. This value picks the first certificate bundle in the ACME alternative chains that has a 
        /// certificate with this value as its issuer’s CN
        /// </summary>
        [JsonProperty(PropertyName = "preferredChain", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "preferredChain", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string PreferredChain { get; set; } = null;

        /// <summary>
        /// Enables or disables validation of the ACME server TLS certificate. If true, requests to the ACME server will not have their TLS 
        /// certificate validated (i.e. insecure connections will be allowed). Only enable this option in development environments. 
        /// The cert-manager system installed roots will be used to verify connections to the ACME server if this is false. Defaults to false.
        /// </summary>
        [JsonProperty(PropertyName = "skipTLSVerify", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "skipTLSVerify", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public bool? SkipTlsVerify { get; set; } = null;

        /// <summary>
        /// ExternalAccountBinding is a reference to a CA external account of the ACME server. If set, upon registration cert-manager will attempt to 
        /// associate the given external account credentials with the registered ACME account.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
        [JsonProperty(PropertyName = "externalAccountBinding", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "externalAccountBinding", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public AcmeExternalAccountBinding ExternalAccountBinding { get; set; } = null;

        /// <summary>
        /// Specifies the private key.
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [JsonProperty(PropertyName = "privateKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "privateKey", ApplyNamingConventions = false, ScalarStyle = YamlDotNet.Core.ScalarStyle.Literal)]
        [DefaultValue(null)]
        public string PrivateKey { get; set; } = null;

        /// <summary>
        /// PrivateKey is the name of a Kubernetes Secret resource that will be used to store the automatically generated ACME account private key. 
        /// Optionally, a key may be specified to select a specific entry within the named Secret resource. If key is not specified, a default of tls.key
        /// will be used.
        /// </summary>
        [JsonProperty(PropertyName = "privateKeySecretRef", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public AcmeSecretKeySelector PrivateKeySecretRef { get; set; } = null;

        /// <summary>
        /// Solvers is a list of challenge solvers that will be used to solve ACME challenges for the matching domains. Solver configurations must be 
        /// provided in order to obtain certificates from an ACME server. For more information, see: https://cert-manager.io/docs/configuration/acme/
        /// </summary>
        [JsonProperty(PropertyName = "solvers", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "solvers", ApplyNamingConventions = false)]
        [DefaultValue(false)]
        public List<AcmeChallengeSolver> Solvers { get; set; } = new List<AcmeChallengeSolver>();

        /// <summary>
        /// Enables or disables generating a new ACME account key. If true, the Issuer resource will not request a new account but will expect the account 
        /// key to be supplied via an existing secret. If false, the cert-manager system will generate a new ACME account key for the Issuer. Defaults to 
        /// false.
        /// </summary>
        [JsonProperty(PropertyName = "disableAccountKeyGeneration", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "disableAccountKeyGeneration", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public bool? DisableAccountKeyGeneration { get; set; } = null;

        /// <summary>
        /// Enables requesting a Not After date on certificates that matches the duration of the certificate. This is not supported by all ACME servers like 
        /// Let’s Encrypt. If set to true when the ACME server does not support it it will create an error on the Order. Defaults to false.
        /// </summary>
        [JsonProperty(PropertyName = "enableDurationFeature", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "enableDurationFeature", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public bool? EnableDurationFeature { get; set; }

        /// <inheritdoc/>
        public void Validate()
        {
            var acmeIssuerPrefix = $"{nameof(AcmeIssuer)}";

            Solvers = Solvers ?? new List<AcmeChallengeSolver>();

            if (!Solvers.Any(solver => solver.Dns01?.Webhook?.SolverName == "neoncluster_io"))
            {
                var neonWebhookSolver = new AcmeIssuerDns01ProviderWebhook()
                {
                    Config = new Dictionary<string, object>()
                {
                    { "Registrar", "route53" }
                },
                    GroupName = "acme.neoncloud.io",
                    SolverName = "neoncluster_io"
                };

                Solvers.Add(new AcmeChallengeSolver()
                {
                    Dns01 = new AcmeChallengeSolverDns01()
                    {
                        Webhook = neonWebhookSolver
                    },
                    Selector = new CertificateDnsNameSelector()
                    {
                        DnsZones = new List<string>() { "neoncluster.io" }
                    }
                });
            }

            foreach (var solver in Solvers)
            {
                solver.Validate();
            }

            PrivateKeySecretRef = PrivateKeySecretRef ?? new AcmeSecretKeySelector()
            {
                Name = "neon-acme-issuer-account-key",
                Key = "tls.key"
            };

            if (ExternalAccountBinding != null)
            {
                ExternalAccountBinding.Validate();
            }
        }
    }
}

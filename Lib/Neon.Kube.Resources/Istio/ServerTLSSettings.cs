//-----------------------------------------------------------------------------
// FILE:	    ServerTLSSettings.cs
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
    /// 
    /// </summary>
    public class ServerTLSSettings: IValidate
    {
        /// <summary>
        /// Initializes a new instance of the ServerTLSSettings class.
        /// </summary>
        public ServerTLSSettings()
        {
        }

        /// <summary>
        /// If set to true, the load balancer will send a 301 redirect for all http connections, asking the clients to use HTTPS.
        /// </summary>
        [JsonProperty(PropertyName = "httpsRedirect", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public bool? HttpsRedirect { get; set; }

        /// <summary>
        /// Indicates whether connections to this port should be secured using TLS. The value of this field determines how TLS is enforced.
        /// </summary>
        [JsonProperty(PropertyName = "mode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
        public TLSMode? Mode { get; set; }

        /// <summary>
        /// The path to the file holding the server-side TLS certificate to use.
        /// </summary>
        /// <remarks>
        /// REQUIRED if <see cref="Mode"/> is <see cref="TLSMode.SIMPLE"/> or <see cref="TLSMode.MUTUAL"/>.
        /// </remarks>
        [JsonProperty(PropertyName = "serverCertificate", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ServerCertificate { get; set; }

        /// <summary>
        /// The path to the file holding the server’s private key.
        /// </summary>
        /// <remarks>
        /// REQUIRED if <see cref="Mode"/> is <see cref="TLSMode.SIMPLE"/> or <see cref="TLSMode.MUTUAL"/>.
        /// </remarks>
        [JsonProperty(PropertyName = "privateKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string PrivateKey { get; set; }

        /// <summary>
        /// The path to a file containing certificate authority certificates to use in verifying a presented client side certificate.
        /// </summary>
        /// <remarks>
        /// REQUIRED if <see cref="Mode"/> is <see cref="TLSMode.MUTUAL"/>.
        /// </remarks>
        [JsonProperty(PropertyName = "caCertificates", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string CaCertificates { get; set; }

        /// <summary>
        /// <para>
        /// For gateways running on Kubernetes, the name of the secret that holds the TLS certs including the CA certificates. Applicable only on 
        /// Kubernetes. The secret (of type generic) should contain the following keys and values: key: <b>privateKey</b> and cert: <b>serverCert</b>. 
        /// For mutual TLS, cacert: <b>CACertificate</b> can be provided in the same secret or a separate secret named <b>secret-cacert</b>. 
        /// Secret of type TLS for server certificates along with ca.crt key for CA certificates is also supported. Only one of server certificates
        /// and CA certificate or credentialName can be specified.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "credentialName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string CredentialName { get; set; }

        /// <summary>
        /// A list of alternate names to verify the subject identity in the certificate presented by the client.
        /// </summary>
        [JsonProperty(PropertyName = "subjectAltNames", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> SubjectAltNames { get; set; }

        /// <summary>
        /// An optional list of base64-encoded SHA-256 hashes of the SKPIs of authorized client certificates. Note: When both verifycertificatehash and 
        /// verifycertificatespki are specified, a hash matching either value will result in the certificate being accepted.
        /// </summary>
        [JsonProperty(PropertyName = "verifyCertificateSpki", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> VerifyCertificateSpki { get; set; }

        /// <summary>
        /// An optional list of hex-encoded SHA-256 hashes of the authorized client certificates. Both simple and colon separated formats are acceptable. 
        /// Note: When both verifycertificatehash and verifycertificatespki are specified, a hash matching either value will result in the certificate
        /// being accepted.
        /// </summary>
        [JsonProperty(PropertyName = "verifyCertificateHash", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> VerifyCertificateHash { get; set; }

        /// <summary>
        /// Minimum TLS protocol version.
        /// </summary>
        [JsonProperty(PropertyName = "minProtocolVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
        public TLSProtocol? MinProtocolVersion { get; set; }

        /// <summary>
        /// Minimum TLS protocol version.
        /// </summary>
        [JsonProperty(PropertyName = "maxProtocolVersion", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
        public TLSProtocol? MaxProtocolVersion { get; set; }

        /// <summary>
        ///  If specified, only support the specified cipher list. Otherwise default to the default cipher list supported by Envoy.
        /// </summary>
        [JsonProperty(PropertyName = "cipherSuites", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> CipherSuites { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
            Covenant.Requires<ArgumentNullException>(!((Mode == TLSMode.SIMPLE || Mode == TLSMode.MUTUAL) && string.IsNullOrEmpty(ServerCertificate)), nameof(ServerCertificate));
            Covenant.Requires<ArgumentNullException>(!((Mode == TLSMode.SIMPLE || Mode == TLSMode.MUTUAL) && string.IsNullOrEmpty(PrivateKey)), nameof(PrivateKey));
            Covenant.Requires<ArgumentNullException>(!((Mode == TLSMode.MUTUAL) && string.IsNullOrEmpty(CaCertificates)), nameof(CaCertificates));
        }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    V1CertificateStatus.cs
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
using System.Reflection;
using System.Runtime.ConstrainedExecution;
using System.Text;

using k8s;
using k8s.Models;
using Neon.JsonConverters;
using Newtonsoft.Json;
using OpenTelemetry.Resources;
using Org.BouncyCastle.Asn1.Ocsp;
using Org.BouncyCastle.Crypto.Tls;
using static System.Net.Mime.MediaTypeNames;

namespace Neon.Kube.Resources.CertManager
{
    /// <summary>
    /// Status of the Certificate. This is set and managed automatically.
    /// </summary>
    public class V1CertificateStatus
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public V1CertificateStatus()
        {
        }

        /// <summary>
        /// List of status conditions to indicate the status of certificates.Known condition types are `Ready` and `Issuing`.
        /// </summary>
        [JsonProperty(PropertyName = "conditions", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<V1Condition> Conditions { get; set; }

        /// <summary>
        /// The expiration time of the certificate stored in the secret named by this resource in `spec.secretName`.
        /// </summary>
        [JsonProperty(PropertyName = "notAfter", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public DateTime? NotAfter { get; set; }

        /// <summary>
        /// The time after which the certificate stored in the secret named by this resource in spec.secretName is valid.
        /// </summary>
        [JsonProperty(PropertyName = "notBefore", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public DateTime? NotBefore { get; set; }

        /// <summary>
        /// The time at which the certificate will be next renewed.If not set, no upcoming renewal is scheduled.
        /// </summary>
        [JsonProperty(PropertyName = "renewalTime", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public DateTime? RenewalTime { get; set; }

        /// <summary>
        /// The time as recorded by the Certificate controller of the most recent failure to complete a CertificateRequest for this Certificate 
        /// resource.If set, cert-manager will not re-request another Certificate until 1 hour has elapsed from this time.
        /// </summary>
        [JsonProperty(PropertyName = "lastFailureTime", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public DateTime? LastFailureTime { get; set; }

        /// <summary>
        /// Controls whether key usages should be present in the CertificateRequest.
        /// </summary>
        [JsonProperty(PropertyName = "revision", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int? Revision { get; set; }

        /// <summary>
        /// The name of the Secret resource containing the private key to be used for the next certificate iteration.The keymanager controller 
        /// will automatically set this field if the `Issuing` condition is set to `True`. It will automatically unset this field when the 
        /// Issuing condition is not set or False.
        /// </summary>
        [JsonProperty(PropertyName = "nextPrivateKeySecretName", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string NextPrivateKeySecretName { get; set; }
    }
}

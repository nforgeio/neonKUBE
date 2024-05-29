//-----------------------------------------------------------------------------
// FILE:        AcmeOptions.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.Kube.Resources.CertManager;
using Neon.Net;
using Neon.Time;
using Neon.Kube.Resources;

namespace Neon.Kube.ClusterDef
{
    /// <summary>
    /// Describes <b>CertManager</b> related options.
    /// </summary>
    public class AcmeOptions
    {
        //---------------------------------------------------------------------
        // Implementation

        private const string    defaultCertificateDuration      = "2160h0m0s";
        private const string    defaultCertificateRenewBefore   = "720h0m0s";

        /// <summary>
        /// Default constructor.
        /// </summary>
        public AcmeOptions()
        {
        }

        /// <summary>
        /// Optionally specifies the maximum lifespan for internal cluster TLS certificates as a GOLANG formatted string.  
        /// This defaults to <b>2160h0m0s</b> (90 days).  See <see cref="GoDuration.Parse(string)"/> for details 
        /// about the timespan format.
        /// </summary>
        [JsonProperty(PropertyName = "CertificateDuration", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "certificateDuration", ApplyNamingConventions = false)]
        [DefaultValue(defaultCertificateDuration)]
        public string CertificateDuration { get; set; } = defaultCertificateDuration;

        /// <summary>
        /// Optionally specifies the time to wait before attempting to renew internal cluster TLS certificates.
        /// This must be less than <see cref="CertificateDuration"/> and defaults to <b>720h0m0s</b> (24 days).
        /// See <see cref="GoDuration.Parse(string)"/> for details about the timespan format.
        /// </summary>
        [JsonProperty(PropertyName = "CertificateRenewBefore", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "certificateRenewBefore", ApplyNamingConventions = false)]
        [DefaultValue(defaultCertificateRenewBefore)]
        public string CertificateRenewBefore { get; set; } = defaultCertificateRenewBefore;

        /// <summary>
        /// Optionally specifies the certificate issuer, including any configuration required by the issuer.
        /// This defaults to <c>null</c> which enables the standard NeonKUBE issuer.
        /// </summary>
        [JsonProperty(PropertyName = "Issuer", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "issuer", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public AcmeIssuer Issuer { get; set; } = null;

        /// <summary>
        /// Validates the options and also ensures that all <c>null</c> properties are
        /// initialized to their default values.
        /// </summary>
        /// <param name="clusterDefinition">The cluster definition.</param>
        /// <exception cref="ClusterDefinitionException">Thrown if the definition is not valid.</exception>
        public void Validate(ClusterDefinition clusterDefinition)
        {
            Covenant.Requires<ArgumentNullException>(clusterDefinition != null, nameof(clusterDefinition));

            var optionsPrefix = $"{nameof(ClusterDefinition.Network.AcmeOptions)}";

            Issuer = Issuer ?? new AcmeIssuer();
            Issuer.Validate();

            // Validate the certificate durations.

            CertificateDuration    ??= defaultCertificateDuration;
            CertificateRenewBefore ??= defaultCertificateRenewBefore;

            if (!GoDuration.TryParse(CertificateDuration, out var duration))
            {
                throw new ClusterDefinitionException($"[{optionsPrefix}.{nameof(CertificateDuration)}={CertificateDuration}] cannot be parsed as a GOLANG duration.");
            }

            if (!GoDuration.TryParse(CertificateRenewBefore, out var renewBefore))
            {
                throw new ClusterDefinitionException($"[{optionsPrefix}.{nameof(CertificateRenewBefore)}={CertificateRenewBefore}] cannot be parsed as a GOLANG duration.");
            }

            if (duration.TimeSpan < TimeSpan.FromSeconds(1))
            {
                throw new ClusterDefinitionException($"[{optionsPrefix}.{nameof(CertificateDuration)}={CertificateDuration}] cannot be less than 1 second.");
            }

            if (renewBefore.TimeSpan < TimeSpan.FromSeconds(1))
            {
                throw new ClusterDefinitionException($"[{optionsPrefix}.{nameof(CertificateRenewBefore)}={CertificateRenewBefore}] cannot be less than 1 second.");
            }

            if (duration.TimeSpan < renewBefore.TimeSpan)
            {
                throw new ClusterDefinitionException($"[{optionsPrefix}.{nameof(CertificateDuration)}={CertificateDuration}] is not greater than or equal to [{optionsPrefix}.{nameof(CertificateRenewBefore)}={CertificateRenewBefore}].");
            }
        }
    }
}

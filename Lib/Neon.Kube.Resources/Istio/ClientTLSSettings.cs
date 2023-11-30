//-----------------------------------------------------------------------------
// FILE:        ClientTLSSettings.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

using Newtonsoft.Json;
using YamlDotNet.Core;

namespace Neon.Kube.Resources.Istio
{
    /// <summary>
    /// Describes the properties of the proxy on a given load balancer port.
    /// </summary>
    public class ClientTLSSettings
    {
        /// <summary>
        /// Initializes a new instance of the ClientTLSSettings class.
        /// </summary>
        public ClientTLSSettings()
        {
        }

        /// <summary>
        /// Indicates whether connections to this port should be secured using TLS. 
        /// The value of this field determines how TLS is enforced.
        /// </summary>
        [JsonProperty(PropertyName = "mode", Required = Required.Always)]
        public TLSMode Mode { get; set; }

        /// <summary>
        /// <para>
        /// InsecureSkipVerify specifies whether the proxy should skip verifying the CA signature and SAN for the server certificate 
        /// corresponding to the host. This flag should only be set if global CA signature verifcation is enabled, VerifyCertAtClient 
        /// environmental variable is set to true, but no verification is desired for a specific host. If enabled with or without 
        /// VerifyCertAtClient enabled, verification of the CA signature and SAN will be skipped.
        /// </para>
        /// <para>
        /// InsecureSkipVerify is false by default. VerifyCertAtClient is false by default in Istio version 1.9 but will be true by 
        /// default in a later version where, going forward, it will be enabled by default.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "insecureSkipVerify", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(false)]
        public bool InsecureSkipVerify { get; set; }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    PrivateKey.cs
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

using Newtonsoft.Json;

namespace Neon.Kube.Resources
{
    /// <summary>
    /// Options to control private keys used for the Certificate.
    /// </summary>
    public class PrivateKey
    {
        /// <summary>
        /// Initializes a new instance of the PrivateKey class.
        /// </summary>
        public PrivateKey()
        {
        }

        /// <summary>
        /// RotationPolicy controls how private keys should be regenerated when a re-issuance is being processed.
        /// Default is 'Never' for backward compatibility.
        /// </summary>
        [JsonProperty(PropertyName = "rotationPolicy", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(RotationPolicy.Never)]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
        public RotationPolicy RotationPolicy { get; set; }

        /// <summary>
        /// The private key cryptography standards (PKCS) encoding for this certificate's private key to be encoded in. 
        /// If provided, allowed values are `PKCS1` and `PKCS8` standing for PKCS#1 and PKCS#8, respectively.
        /// Defaults to `PKCS1` if not specified.
        /// </summary>
        [JsonProperty(PropertyName = "encoding", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(KeyEncoding.PKCS1)]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
        public KeyEncoding Encoding { get; set; }

        /// <summary>
        /// The private key algorithm of the corresponding private key for this certificate.
        /// </summary>
        [JsonProperty(PropertyName = "algorithm", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(KeyAlgorithm.RSA)]
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumMemberConverter))]
        public KeyAlgorithm Algorithm { get; set; }

        /// <summary>
        /// The private key algorithm of the corresponding private key for this certificate.
        /// </summary>
        [JsonProperty(PropertyName = "size", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int? Size { get; set; }
    }
}

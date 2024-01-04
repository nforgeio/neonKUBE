//-----------------------------------------------------------------------------
// FILE:        Keystores.cs
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
using System.Linq;
using System.Text;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube.Resources.CertManager
{
    /// <summary>
    /// Configures additional keystore output formats stored in the `secretName` Secret resource.
    /// </summary>
    public class Keystores
    {
        /// <summary>
        /// Initializes a new instance of the Keystores class.
        /// </summary>
        public Keystores()
        {
        }

        /// <summary>
        /// Configures options for storing a JKS keystore in the `spec.secretName` Secret resource.
        /// </summary>
        [JsonProperty(PropertyName = "jks")]
        public string Jks { get; set; }

        /// <summary>
        /// Configures options for storing a PKCS12 keystore in the `spec.secretName` Secret resource.
        /// </summary>
        [JsonProperty(PropertyName = "pkcs12")]
        public string Pkcs12 { get; set; }
    }
}

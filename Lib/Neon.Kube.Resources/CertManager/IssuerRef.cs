//-----------------------------------------------------------------------------
// FILE:	    IssuerRef.cs
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
using System.Text;

using k8s;
using k8s.Models;

using Newtonsoft.Json;

namespace Neon.Kube.Resources.CertManager
{
    /// <summary>
    /// A reference to the issuer for this certificate.
    /// If the `kind` field is not set, or set to `Issuer`, an Issuer resource with the given name in the 
    /// same namespace as the Certificate will be used.If the `kind` field is set to `ClusterIssuer`, 
    /// a ClusterIssuer with the provided name will be used.The `name` field in this stanza is required at 
    /// all times.
    /// </summary>
    public class IssuerRef
    {
        /// <summary>
        /// Initializes a new instance of the IssuerRef class.
        /// </summary>
        public IssuerRef()
        {
        }

        /// <summary>
        /// Group of the resource being referred to.
        /// </summary>
        [JsonProperty(PropertyName = "group", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string Group { get; set; }

        /// <summary>
        /// Kind of the resource being referred to.
        /// </summary>
        [JsonProperty(PropertyName = "kind", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Kind { get; set; }

        /// <summary>
        /// Name of the resource being referred to.
        /// </summary>
        [JsonProperty(PropertyName = "name", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Name { get; set; }
    }
}

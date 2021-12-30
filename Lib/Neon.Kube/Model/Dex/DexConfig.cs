//-----------------------------------------------------------------------------
// FILE:	    DexConfig.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

namespace Neon.Kube
{
    /// <summary>
    /// Dex configuration model.
    /// </summary>
    public class DexConfig
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexConfig()
        {
        }

        /// <summary>
        /// The base path of dex and the external name of the OpenID Connect service.
        /// This is the canonical URL that all clients MUST use to refer to dex. If a
        /// path is provided, dex's HTTP service will listen at a non-root URL.
        /// </summary>
        [JsonProperty(PropertyName = "issuer", Required = Required.Always)]
        [DefaultValue(null)]
        public string Issuer { get; set; }

        /// <summary>
        /// The storage configuration determines where dex stores its state. Supported
        /// options include SQL flavors and Kubernetes third party resources. 
        /// See the documentation (https://dexidp.io/docs/storage/) for further information.
        /// </summary>
        [JsonProperty(PropertyName = "storage", Required = Required.Always)]
        [DefaultValue(null)]
        public DexStorage Storage { get; set; }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    DexWebConfig.cs
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
    /// Configuration for the HTTP endpoints.
    /// </summary>
    public class DexWebConfig
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DexWebConfig()
        {
        }

        /// <summary>
        /// Http Endpoint.
        /// </summary>
        [JsonProperty(PropertyName = "http", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Http { get; set; }

        /// <summary>
        /// Https Endpoint.
        /// </summary>
        [JsonProperty(PropertyName = "https", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Https { get; set; }

        /// <summary>
        /// Reference to TLS certificate file.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "tlsCert", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string TlsCert { get; set; }

        /// <summary>
        /// Reference to TLS certificate key file.
        /// information.
        /// </summary>
        [JsonProperty(PropertyName = "tlsKey", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string TlsKey { get; set; }
    }
}
//-----------------------------------------------------------------------------
// FILE:	    PrometheusResponse.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;
using Neon.IO;

namespace Neon.Kube
{
    /// <summary>
    /// Models the Prometheus HTTP API response.
    /// </summary>
    public class PrometheusResponse<T>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public PrometheusResponse() 
        {

        }

        /// <summary>
        /// Specifies the status of the request.
        /// </summary>
        [JsonProperty(PropertyName = "status", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.Include)]
        public PrometheusResponseStatus Status { get; set; }

        /// <summary>
        /// Describes the error type.
        /// </summary>
        [JsonProperty(PropertyName = "data", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public T Data { get; set; }

        /// <summary>
        /// Describes the error type.
        /// </summary>
        [JsonProperty(PropertyName = "errorType", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string ErrorType { get; set; } = null;

        /// <summary>
        /// Describes the error.
        /// </summary>
        [JsonProperty(PropertyName = "error", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Error { get; set; } = null;

        /// <summary>
        /// Lists warnings.
        /// </summary>
        [JsonProperty(PropertyName = "warnings", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public List<string> Warnings { get; set; } = null;
    }
}

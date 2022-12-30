//-----------------------------------------------------------------------------
// FILE:	    HTTPRetry.cs
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
    /// Describes the retry policy to use when a HTTP request fails. For example, the following rule sets the maximum number of retries to 3 
    /// when calling ratings:v1 service, with a 2s timeout per retry attempt.
    /// </summary>
    public class HTTPRetry : IValidate
    {
        /// <summary>
        /// Initializes a new instance of the HTTPRetry class.
        /// </summary>
        public HTTPRetry()
        {
        }

        /// <summary>
        /// <para>>
        /// Number of retries to be allowed for a given request. The interval between retries will be determined automatically (25ms+). 
        /// When request timeout of the HTTP route or per_try_timeout is configured, the actual number of retries attempted also depends on 
        /// the specified request timeout and per_try_timeout values.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "attempts", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int? Attempts { get; set; }

        /// <summary>
        /// <para>
        /// Timeout per attempt for a given request, including the initial call and any retries. Format: 1h/1m/1s/1ms. MUST BE >=1ms. Default is same 
        /// value as request timeout of the HTTP route, which means no timeout.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "perTryTimeout", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string PerTryTimeout { get; set; }

        /// <summary>
        /// <para>
        /// Specifies the conditions under which retry takes place. One or more policies can be specified using a ‘,’ delimited list. See the retry 
        /// policies and gRPC retry policies for more details.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "retryOn", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        public string retryOn { get; set; }

        /// <summary>
        /// <para>
        /// Flag to specify whether the retries should retry to other localities. See the retry plugin configuration for more details.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "retryRemoteLocalities", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public bool? RetryRemoteLocalities { get; set; }
        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
        }
    }
}

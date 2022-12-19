//-----------------------------------------------------------------------------
// FILE:	    HTTPFaultInjection.Abort.cs
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
    /// <para>
    /// Abort specification is used to prematurely abort a request with a pre-specified error code. The following example will return an HTTP 
    /// 400 error code for 1 out of every 1000 requests to the “ratings” service “v1”.
    /// </para>
    /// </summary>
    public class Abort : IValidate
    {
        /// <summary>
        /// Initializes a new instance of the HTTPFaultInjection.Abort class.
        /// </summary>
        public Abort()
        {
        }

        /// <summary>
        /// <para>
        /// HTTP status code to use to abort the Http request.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "httpStatus", Required = Required.Always, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int HTTPStatus { get; set; }

        /// <summary>
        /// <para>
        /// Percentage of requests to be aborted with the error code provided.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "percentage", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public Percent Percentage { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
        }
    }
}

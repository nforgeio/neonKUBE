//-----------------------------------------------------------------------------
// FILE:	    HTTPRewrite.cs
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
    /// HTTPRewrite can be used to rewrite specific parts of a HTTP request before forwarding the request to the destination. Rewrite 
    /// primitive can be used only with HTTPRouteDestination. The following example demonstrates how to rewrite the URL prefix for api 
    /// call (/ratings) to ratings service before making the actual API call.
    /// </summary>
    public class HTTPRewrite : IValidate
    {
        /// <summary>
        /// Initializes a new instance of the HTTPRewrite class.
        /// </summary>
        public HTTPRewrite()
        {
        }

        /// <summary>
        /// <para>
        /// Rewrite the path (or the prefix) portion of the URI with this value. If the original URI was matched based on prefix, the 
        /// value provided in this field will replace the corresponding matched prefix.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "uri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Uri { get; set; }

        /// <summary>
        /// Rewrite the Authority/Host header with this value.
        /// </summary>
        [JsonProperty(PropertyName = "authority", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Authority { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
        }
    }
}

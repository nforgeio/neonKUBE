//-----------------------------------------------------------------------------
// FILE:	    HTTPRedirect.cs
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
    /// HTTPRedirect can be used to send a 301 redirect response to the caller, where the Authority/Host and the URI in the response can be 
    /// swapped with the specified values. For example, the following rule redirects requests for /v1/getProductRatings API on the ratings 
    /// service to /v1/bookRatings provided by the bookratings service.
    /// </summary>
    public class HTTPRedirect : IValidate
    {
        /// <summary>
        /// Initializes a new instance of the HTTPRedirect class.
        /// </summary>
        public HTTPRedirect()
        {
        }

        /// <summary>
        /// On a redirect, overwrite the Path portion of the URL with this value. Note that the entire path will be replaced, irrespective 
        /// of the request URI being matched as an exact path or prefix.
        /// </summary>
        [JsonProperty(PropertyName = "uri", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Uri { get; set; }

        /// <summary>
        /// <para>
        /// On a redirect, overwrite the Authority/Host portion of the URL with this value.
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "authority", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Authority { get; set; }

        /// <summary>
        /// <para>
        /// On a redirect, Specifies the HTTP status code to use in the redirect response. The default response code is MOVED_PERMANENTLY (301).
        /// </para>
        /// </summary>
        [JsonProperty(PropertyName = "redirectCode", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public int? RedirectCode { get; set; }

        /// <summary>
        /// Validate the object.
        /// </summary>
        /// <exception cref="ValidationException">Thrown if validation fails.</exception>
        public virtual void Validate()
        {
        }
    }
}

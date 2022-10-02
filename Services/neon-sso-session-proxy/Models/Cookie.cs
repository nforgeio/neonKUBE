//-----------------------------------------------------------------------------
// FILE:	    Cookie.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

namespace NeonSsoSessionProxy
{
    /// <summary>
    /// 
    /// </summary>
    public class Cookie
    {
        /// <summary>
        /// OAuth 2.0 Client Identifier valid at the Authorization Server.
        /// </summary>
        [JsonProperty(PropertyName = "ClientId", Required = Required.Default)]
        [DefaultValue(null)]
        public string ClientId { get; set; }

        /// <summary>
        /// Opaque value used to maintain state between the request and the callback. 
        /// Typically, Cross-Site Request Forgery (CSRF, XSRF) mitigation is done by 
        /// cryptographically binding the value of this parameter with a browser cookie.
        /// </summary>
        [JsonProperty(PropertyName = "State", Required = Required.Default)]
        [DefaultValue(null)]
        public string State { get; set; }

        /// <summary>
        /// OAuth 2.0 Authorization Code
        /// </summary>
        [JsonProperty(PropertyName = "Code", Required = Required.Default)]
        [DefaultValue(null)]
        public string Code { get; set; }

        /// <summary>
        /// OAuth 2.0 Redirect Uri
        /// </summary>
        [JsonProperty(PropertyName = "RedirectUri", Required = Required.Default)]
        [DefaultValue(null)]
        public string RedirectUri { get; set; }

        /// <summary>
        /// OAuth 2.0 Access Type.
        /// </summary>
        [JsonProperty(PropertyName = "AccessType", Required = Required.Default)]
        [DefaultValue(null)]
        public string AccessType { get; set; }

        /// <summary>
        /// OAuth 2.0 Response Type.
        /// </summary>
        [JsonProperty(PropertyName = "ResponseType", Required = Required.Default)]
        [DefaultValue(null)]
        public string ResponseType { get; set; }

        /// <summary>
        /// OAuth 2.0 Scope.
        /// </summary>
        [JsonProperty(PropertyName = "Scope", Required = Required.Default)]
        [DefaultValue(null)]
        public string Scope { get; set; }

        /// <summary>
        /// OAuth 2.0 Scope.
        /// </summary>
        [JsonProperty(PropertyName = "TokenResponse", Required = Required.Default)]
        [DefaultValue(null)]
        public TokenResponse TokenResponse { get; set; }
    }
}

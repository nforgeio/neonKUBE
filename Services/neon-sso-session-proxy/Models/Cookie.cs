//-----------------------------------------------------------------------------
// FILE:	    Cookie.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.


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

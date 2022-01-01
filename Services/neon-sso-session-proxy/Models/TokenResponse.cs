//-----------------------------------------------------------------------------
// FILE:	    TokenResponse.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.

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
    public class TokenResponse
    {
        /// <summary>
        /// Access Token for the UserInfo Endpoint.
        /// </summary>
        [JsonProperty(PropertyName = "access_token", Required = Required.Default)]
        [DefaultValue(null)]
        public string AccessToken { get; set; }

        /// <summary>
        /// OAuth 2.0 Token Type value. The value MUST be Bearer, as specified in OAuth 
        /// 2.0 Bearer Token Usage [RFC6750], for Clients using this subset. Note 
        /// that the token_type value is case insensitive.
        /// </summary>
        [JsonProperty(PropertyName = "token_type", Required = Required.Default)]
        [DefaultValue(null)]
        public string TokenType { get; set; }

        /// <summary>
        /// Expiration time of the Access Token in seconds since the response was generated.
        /// </summary>
        [JsonProperty(PropertyName = "expires_in", Required = Required.Default)]
        [DefaultValue(null)]
        public int? ExpiresIn { get; set; }

        /// <summary>
        /// ID Token.
        /// </summary>
        [JsonProperty(PropertyName = "id_token", Required = Required.Default)]
        [DefaultValue(null)]
        public string IdToken { get; set; }

        /// <summary>
        /// Refresh Token.
        /// </summary>
        [JsonProperty(PropertyName = "refresh_token", Required = Required.Default)]
        [DefaultValue(null)]
        public string RefreshToken { get; set; }
    }
}

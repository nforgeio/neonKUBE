//-----------------------------------------------------------------------------
// FILE:	    Credentials.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;

using Neon.Common;

namespace Neon.Common
{
    /// <summary>
    /// Used to persist credentials.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two forms of credentials are currently supported: a standalone security token or
    /// API key or the combination of a username and password.
    /// </para>
    /// </remarks>
    public class Credentials
    {
        /// <summary>
        /// The security token.
        /// </summary>
        [JsonProperty(PropertyName = "Token", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "token", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Token { get; set; }

        /// <summary>
        /// The username (use in conjunction with <see cref="Password"/>).
        /// </summary>
        [JsonProperty(PropertyName = "Username", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "username", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Username { get; set; }

        /// <summary>
        /// The password (use in conjunction with <see cref="Username"/>).
        /// </summary>
        [JsonProperty(PropertyName = "Password", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [YamlMember(Alias = "password", ApplyNamingConventions = false)]
        [DefaultValue(null)]
        public string Password { get; set; }

        /// <summary>
        /// Returns <c>true</c> if the credentials hold a <see cref="Token"/>.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool HasToken
        {
            get { return !string.IsNullOrEmpty(Token); }
        }

        /// <summary>
        /// Returns <c>true</c> if the credentials hold a <see cref="Username"/> and <see cref="Password"/>.
        /// </summary>
        [JsonIgnore]
        [YamlIgnore]
        public bool HasUsernamePassword
        {
            get { return !string.IsNullOrEmpty(Username) && Password != null; }
        }
    }
}

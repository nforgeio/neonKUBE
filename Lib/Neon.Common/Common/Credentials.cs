//-----------------------------------------------------------------------------
// FILE:	    Credentials.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
using Neon.Cryptography;

namespace Neon.Common
{
    /// <summary>
    /// Used to persist database and other credentials as a Docker service secret.
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
        [DefaultValue(null)]
        public string Token { get; set; }

        /// <summary>
        /// The username (use in conjunction with <see cref="Password"/>).
        /// </summary>
        [JsonProperty(PropertyName = "Username", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public string Username { get; set; }

        /// <summary>
        /// The password (use in conjunction with <see cref="Username"/>).
        /// </summary>
        [JsonProperty(PropertyName = "Password", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
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

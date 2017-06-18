//-----------------------------------------------------------------------------
// FILE:	    Credentials.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

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
        public string Token { get; set; }

        /// <summary>
        /// The username (use in conjunction with <see cref="Password"/>).
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        /// The password (use in conjunction with <see cref="Username"/>).
        /// </summary>
        public string Password { get; set; }
    }
}

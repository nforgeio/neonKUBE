//-----------------------------------------------------------------------------
// FILE:	    AuthMethods.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;

namespace Neon.Cluster
{
    /// <summary>
    /// Enumerates the supported authentication methods.
    /// </summary>
    public enum AuthMethods
    {
        /// <summary>
        /// Username/password authentication.
        /// </summary>
        [EnumMember(Value = "password")]
        Password,

        /// <summary>
        /// Mutual TLS authentication using certificates and private keys.
        /// </summary>
        [EnumMember(Value = "tls")]
        Tls
    }
}

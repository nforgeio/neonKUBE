//-----------------------------------------------------------------------------
// FILE:	    ConsulListMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Net;

namespace Consul
{
    /// <summary>
    /// Enumerates the possible <see cref="ConsulExtensions.ListKeys(IKVEndpoint, string, ConsulListMode, CancellationToken)"/> 
    /// key listing modes.
    /// </summary>
    public enum ConsulListMode
    {
        /// <summary>
        /// Returns the fully qualified keys at the level specified
        /// by the key prefix.  This is the default.
        /// </summary>
        FullKey = 0,

        /// <summary>
        /// Returns the fully qualified keys at the level specified
        /// by the key prefix as well as those at all levels below.
        /// </summary>
        FullKeyRecursive,

        /// <summary>
        /// Returns only the keys at the current level without
        /// the prefix.
        /// </summary>
        PartialKey
    }
}

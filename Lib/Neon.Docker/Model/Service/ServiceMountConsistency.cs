//-----------------------------------------------------------------------------
// FILE:	    ServiceMountConsistency.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Docker
{
    /// <summary>
    /// Enumerates the service mount consistency options
    /// </summary>
    public enum ServiceMountConsistency
    {
        /// <summary>
        /// Default consistency.
        /// </summary>
        [EnumMember(Value = "default")]
        Default,

        /// <summary>
        /// Consistent.
        /// </summary>
        [EnumMember(Value = "consistent")]
        Consistent,

        /// <summary>
        /// Cached.
        /// </summary>
        [EnumMember(Value = "cached")]
        Cached,

        /// <summary>
        /// Delegated.
        /// </summary>
        [EnumMember(Value = "delegated")]
        Delegated
    }
}

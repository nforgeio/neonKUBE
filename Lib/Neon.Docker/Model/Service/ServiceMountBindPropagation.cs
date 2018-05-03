//-----------------------------------------------------------------------------
// FILE:	    ServiceMountBindPropagation.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Docker
{
    /// <summary>
    /// Enumerates the mount propagation options.
    /// </summary>
    public enum ServiceMountBindPropagation
    {
        /// <summary>
        /// RPrivate.
        /// </summary>
        [EnumMember(Value = "rprivate")]
        RPrivate = 0,

        /// <summary>
        /// Shared.
        /// </summary>
        [EnumMember(Value = "shared")]
        Shared,

        /// <summary>
        /// Slave.
        /// </summary>
        [EnumMember(Value = "slave")]
        Slave,

        /// <summary>
        /// Private.
        /// </summary>
        [EnumMember(Value = "private")]
        Private,

        /// <summary>
        /// RShared.
        /// </summary>
        [EnumMember(Value = "rshared")]
        RShared,

        /// <summary>
        /// RSlave.
        /// </summary>
        [EnumMember(Value = "rslave")]
        RSlave
    }
}

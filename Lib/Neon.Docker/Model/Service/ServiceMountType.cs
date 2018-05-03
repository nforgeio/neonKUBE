//-----------------------------------------------------------------------------
// FILE:	    ServiceMountType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Docker
{
    /// <summary>
    /// Enumerates the service mount types.
    /// </summary>
    public enum ServiceMountType
    {
        /// <summary>
        /// Mount a Docker volume.
        /// </summary>
        [EnumMember(Value = "volume")]
        Volume = 0,

        /// <summary>
        /// Mount a directory from the Docker host.
        /// </summary>
        [EnumMember(Value = "bind")]
        Bind,

        /// <summary>
        /// Create and mount a tmpfs.
        /// </summary>
        [EnumMember(Value = "tmpfs")]
        Tmpfs
    }
}

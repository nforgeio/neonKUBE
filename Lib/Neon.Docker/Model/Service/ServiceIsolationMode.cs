//-----------------------------------------------------------------------------
// FILE:	    ServiceIsolationMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Docker
{
    /// <summary>
    /// <b>Windows Only:</b> Enumerates the isolation technologies
    /// to be used for the service containers.
    /// </summary>
    public enum ServiceIsolationMode
    {
        /// <summary>
        /// Use the default mode.
        /// </summary>
        [EnumMember(Value = "default")]
        Default = 0,

        /// <summary>
        /// Use process isolation.
        /// </summary>
        [EnumMember(Value = "process")]
        Process,

        /// <summary>
        /// User Hyper-V isolation.
        /// </summary>
        [EnumMember(Value = "hyperv")]
        HyperV
    }
}

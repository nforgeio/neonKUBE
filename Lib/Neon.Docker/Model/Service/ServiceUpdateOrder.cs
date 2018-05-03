//-----------------------------------------------------------------------------
// FILE:	    ServiceUpdateOrder.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Docker
{
    /// <summary>
    /// Enumerates the service taek update rollback order options.
    /// </summary>
    public enum ServiceUpdateOrder
    {
        /// <summary>
        /// Stop a service task first and then start its replacement.
        /// </summary>
        [EnumMember(Value = "stop-first")]
        StopFirst = 0,

        /// <summary>
        /// Start a service replacement task first and before stopping
        /// the original task.
        /// </summary>
        [EnumMember(Value = "start-first")]
        StartFirst
    }
}

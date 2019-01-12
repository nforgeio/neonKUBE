//-----------------------------------------------------------------------------
// FILE:	    ServiceRollbackOrder.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Docker
{
    /// <summary>
    /// Enumerates the service task rollback order options.
    /// </summary>
    public enum ServiceRollbackOrder
    {
        /// <summary>
        /// Stop the current service task before rolling back to the 
        /// previous settings.
        /// </summary>
        [EnumMember(Value = "stop-first")]
        StopFirst = 0,

        /// <summary>
        /// Rollback a current service task to the previous setting first
        /// before stopping the current task.
        /// </summary>
        [EnumMember(Value = "start-first")]
        StartFirst
    }
}

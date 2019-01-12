//-----------------------------------------------------------------------------
// FILE:	    ServiceUpdateState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Docker
{
    /// <summary>
    /// Enumerates the service update states.
    /// </summary>
    public enum ServiceUpdateState
    {
        /// <summary>
        /// Service update has completed.
        /// </summary>
        [EnumMember(Value = "completed")]
        Completed = 0,

        /// <summary>
        /// Service is actively being updated.
        /// </summary>
        [EnumMember(Value = "updating")]
        Updating,

        /// <summary>
        /// Service update is paused.
        /// </summary>
        [EnumMember(Value = "paused")]
        Paused,

        /// <summary>
        /// Service update has completed.
        /// </summary>
        [EnumMember(Value = "rollback_completed")]
        RollbackCompleted = 0,
    }
}

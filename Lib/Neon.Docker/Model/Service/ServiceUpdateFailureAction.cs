//-----------------------------------------------------------------------------
// FILE:	    ServiceUpdateFailureAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Docker
{
    /// <summary>
    /// Enumerates the service update failure actions.
    /// </summary>
    public enum ServiceUpdateFailureAction
    {
        /// <summary>
        /// Pause scheduling updated service tasks on failure.
        /// </summary>
        [EnumMember(Value = "pause")]
        Pause = 0,

        /// <summary>
        /// Continue scheduling updated service tasks on failure.
        /// </summary>
        [EnumMember(Value = "continue")]
        Continue,

        /// <summary>
        /// Rollback the service to the previous state on failure.
        /// </summary>
        [EnumMember(Value = "rollback")]
        Rollback
    }
}

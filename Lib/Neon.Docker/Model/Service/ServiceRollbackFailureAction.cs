//-----------------------------------------------------------------------------
// FILE:	    ServiceRollbackFailureAction.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Docker
{
    /// <summary>
    /// Enumerates the service rollback failure actions.
    /// </summary>
    public enum ServiceRollbackFailureAction
    {
        /// <summary>
        /// Pause the service task rollback on failure.
        /// </summary>
        [EnumMember(Value = "pause")]
        Pause = 0,

        /// <summary>
        /// Continue the service task rollback on failure.
        /// </summary>
        [EnumMember(Value = "continue")]
        Continue,
    }
}

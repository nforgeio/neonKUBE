//-----------------------------------------------------------------------------
// FILE:	    ServiceRestartCondition.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Docker
{
    /// <summary>
    /// Enumerates the service restart conditions.
    /// </summary>
    public enum ServiceRestartCondition
    {
        /// <summary>
        /// Restart whenever a service task exits for any reason.
        /// </summary>
        [EnumMember(Value = "any")]
        Any = 0,

        /// <summary>
        /// Never restart.
        /// </summary>
        [EnumMember(Value = "none")]
        None,

        /// <summary>
        /// Restart only when a service task returns a non zero exit code.
        /// </summary>
        [EnumMember(Value = "on-failure")]
        OnFailure
    }

}

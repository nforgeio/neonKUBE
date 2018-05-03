//-----------------------------------------------------------------------------
// FILE:	    ServiceMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Docker
{
    /// <summary>
    /// Enumerates the service modes.
    /// </summary>
    public enum ServiceMode
    {
        /// <summary>
        /// Service should deploy a specified number of replicas on nodes
        /// that satisfy the constraints.
        /// </summary>
        [EnumMember(Value = "replicated")]
        Replicated = 0,

        /// <summary>
        /// Service should deploy on all hosts that satisfy the constraints.
        /// </summary>
        [EnumMember(Value = "global")]
        Global
    }
}

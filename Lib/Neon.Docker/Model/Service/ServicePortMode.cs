//-----------------------------------------------------------------------------
// FILE:	    ServicePortMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Docker
{
    /// <summary>
    /// Enumerates the service port modes.
    /// </summary>
    public enum ServicePortMode
    {
        /// <summary>
        /// Publish service ports to the Docker Swarm ingress mesh network.
        /// </summary>
        [EnumMember(Value = "ingress")]
        Ingress = 0,

        /// <summary>
        /// Publish service ports to the local Docker host network.
        /// </summary>
        [EnumMember(Value = "host")]
        Host
    }
}

//-----------------------------------------------------------------------------
// FILE:	    ServicePortProtocol.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Docker
{
    /// <summary>
    /// Enumerates the service port protocols.
    /// </summary>
    public enum ServicePortProtocol
    {
        /// <summary>
        /// TCP
        /// </summary>
        [EnumMember(Value = "tcp")]
        Tcp = 0,

        /// <summary>
        /// UDP
        /// </summary>
        [EnumMember(Value = "udp")]
        Udp
    }
}

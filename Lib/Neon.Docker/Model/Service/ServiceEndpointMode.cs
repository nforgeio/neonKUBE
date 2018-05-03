//-----------------------------------------------------------------------------
// FILE:	    ServiceEndpointMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace Neon.Docker
{
    /// <summary>
    /// Service endpoint mode.
    /// </summary>
    public enum ServiceEndpointMode
    {
        /// <summary>
        /// Assign a virtual IP address to the service and provide a load balancer.
        /// </summary>
        [EnumMember(Value = "vip")]
        Vip = 0,

        /// <summary>
        /// Returns DNS resource records for the active service instances.
        /// </summary>
        [EnumMember(Value = "dnsrr")]
        DnsRR
    }
}

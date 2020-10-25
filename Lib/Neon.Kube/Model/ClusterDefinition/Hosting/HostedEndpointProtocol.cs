//-----------------------------------------------------------------------------
// FILE:	    HostedEndpointProtocol.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE LLC.  All rights reserved.

using System;

namespace Neon.Kube
{
    /// <summary>
    /// Enumerates the possible <see cref="HostedEndpoint"/> protocols.
    /// </summary>
    public enum HostedEndpointProtocol
    {
        /// <summary>
        /// TCP protocol.
        /// </summary>
        Tcp,

        /// <summary>
        /// UDP protocol.
        /// </summary>
        Udp
    }
}

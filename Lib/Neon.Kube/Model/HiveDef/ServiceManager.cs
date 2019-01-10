//-----------------------------------------------------------------------------
// FILE:	    ServiceManager.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;

namespace Neon.Kube
{
    /// <summary>
    /// Identifies the service manager configured for a Linux node.
    /// </summary>
    public enum ServiceManager
    {
        /// <summary>
        /// Systemd
        /// </summary>
        Systemd
    }
}

//-----------------------------------------------------------------------------
// FILE:	    KubeHostPorts.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Kube
{
    /// <summary>
    /// Defines reserved local node and cluster network ports.
    /// </summary>
    public static class KubeHostPorts
    {
        /// <summary>
        /// This port is reserved and must not be assigned to any service.  This is
        /// currently referenced by the manager traffic manager rule for Azure deployments
        /// and it must not actually host a service.  See the <b>AzureHostingManager</b>
        /// source code for more information.
        /// </summary>
        public const int ReservedUnused = 5099;
    }
}

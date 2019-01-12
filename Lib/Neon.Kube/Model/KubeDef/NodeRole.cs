//-----------------------------------------------------------------------------
// FILE:	    NodeRole.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Runtime.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Enumerates the roles a Docker node can assume.
    /// </summary>
    public static class NodeRole
    {
        /// <summary>
        /// The node is a a cluster master.
        /// </summary>
        public const string Master = "master";

        /// <summary>
        /// The node is a cluster worker.
        /// </summary>
        public const string Worker = "worker";
    }
}

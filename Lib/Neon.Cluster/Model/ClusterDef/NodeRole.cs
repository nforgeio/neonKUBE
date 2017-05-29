//-----------------------------------------------------------------------------
// FILE:	    NodeRole.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Runtime.Serialization;

namespace Neon.Cluster
{
    /// <summary>
    /// Enumerates the roles a Docker node can assume.
    /// </summary>
    public static class NodeRole
    {
        /// <summary>
        /// The node is a a cluster manager.
        /// </summary>
        public const string Manager = "manager";

        /// <summary>
        /// The node is a cluster worker.
        /// </summary>
        public const string Worker = "worker";
    }
}

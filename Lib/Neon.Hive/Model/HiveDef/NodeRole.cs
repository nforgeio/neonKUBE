//-----------------------------------------------------------------------------
// FILE:	    NodeRole.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Runtime.Serialization;

namespace Neon.Hive
{
    /// <summary>
    /// Enumerates the roles a Docker node can assume.
    /// </summary>
    public static class NodeRole
    {
        /// <summary>
        /// The node is a a hive manager.
        /// </summary>
        public const string Manager = "manager";

        /// <summary>
        /// The node is a hive worker.
        /// </summary>
        public const string Worker = "worker";

        /// <summary>
        /// The node is a member of the neonHIVE but is not part of the Docker Swarm.
        /// </summary>
        public const string Pet = "pet";
    }
}

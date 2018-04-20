//-----------------------------------------------------------------------------
// FILE:	    DockerSwarmFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

using Neon.Common;

namespace Xunit
{
    /// <summary>
    /// Used to manage local Swarm activities in unit tests.
    /// </summary>
    /// <threadsafety instance="false"/>
    public class DockerSwarmFixture : TestFixture
    {
        /// <summary>
        /// Constructs the fixture.
        /// </summary>
        public DockerSwarmFixture()
        {
        }

        /// <summary>
        /// Finalizer.
        /// </summary>
        ~DockerSwarmFixture()
        {
            Dispose(false);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected override void Dispose(bool disposing)
        {
            if (!base.IsDisposed)
            {
            }
        }
    }
}

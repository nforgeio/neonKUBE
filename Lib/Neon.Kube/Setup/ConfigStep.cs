//-----------------------------------------------------------------------------
// FILE:	    ConfigStep.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Kube
{
    /// <summary>
    /// The <c>abstract</c> base class for node configuration step implementations.
    /// </summary>
    public abstract class ConfigStep
    {
        /// <summary>
        /// Implements the configuration step.
        /// </summary>
        /// <param name="cluster">The cluster proxy instance.</param>
        public abstract void Run(KubeProxy cluster);

        /// <summary>
        /// Pause briefly to allow the configuration UI a chance to display
        /// step information.
        /// </summary>
        protected void StatusPause()
        {
            Thread.Sleep(TimeSpan.FromMilliseconds(500));
        }
    }
}

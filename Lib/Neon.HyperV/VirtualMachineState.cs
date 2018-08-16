//-----------------------------------------------------------------------------
// FILE:	    VirtualMachineState.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.HyperV
{
    /// <summary>
    /// Enumerates the known Hyper-V virtual machine states.
    /// </summary>
    public enum VirtualMachineState
    {
        /// <summary>
        /// The current state cannot be determined or is not one of
        /// the known states below.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The machine is turned off.
        /// </summary>
        Off,

        /// <summary>
        /// The machine is starting.
        /// </summary>
        Starting,

        /// <summary>
        /// The machine is running.
        /// </summary>
        Running,

        /// <summary>
        /// The machine is paused.
        /// </summary>
        Paused,

        /// <summary>
        /// The machine saved.
        /// </summary>
        Saved
    }
}

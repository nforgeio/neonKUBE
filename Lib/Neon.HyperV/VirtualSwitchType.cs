//-----------------------------------------------------------------------------
// FILE:	    VirtualSwitchType.cs
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
    public enum VirtualSwitchType
    {
        /// <summary>
        /// The current switch type cannot be determined or is not one of
        /// the known states below.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The switch can communicate with the host operating system as well as
        /// any networks the host can reach.
        /// </summary>
        External,

        /// <summary>
        /// The switch can communicate with the host operating system as well as
        /// any hosted virtual machines connected to an <see cref="External"/>
        /// or <see cref="Internal"/> switch.  The switch cannot communicate
        /// with anything outside of the host.
        /// </summary>
        Internal,

        /// <summary>
        /// The switch can communicate only with virtual machines using the
        /// same switch.
        /// </summary>
        Private
    }
}

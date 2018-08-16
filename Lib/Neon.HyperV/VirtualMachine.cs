//-----------------------------------------------------------------------------
// FILE:	    VirtualMachine.cs
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
    /// Describes the state of a Hyper-V based virtual machine.
    /// </summary>
    public class VirtualMachine
    {
        /// <summary>
        /// The machine name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The current machine state.
        /// </summary>
        public VirtualMachineState State { get; set; }
    }
}

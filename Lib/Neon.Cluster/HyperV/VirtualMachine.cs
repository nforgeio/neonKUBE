//-----------------------------------------------------------------------------
// FILE:	    VirtualMachine.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Cluster.HyperV
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

        /// <summary>
        /// Returns the file system paths of the virtual hard drives attached
        /// to the the machine.
        /// </summary>
        public List<string> DrivePaths { get; set; } = new List<string>();
    }
}

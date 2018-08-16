//-----------------------------------------------------------------------------
// FILE:	    VirtualSwitch.cs
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
    /// Describes the a Hyper-V virtual network switch.
    /// </summary>
    public class VirtualSwitch
    {
        /// <summary>
        /// The switch name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The switch type.
        /// </summary>
        public VirtualSwitchType Type { get; set; }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    VirtualDrive.cs
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
    /// Specifies virtual drive creation parameters.
    /// </summary>
    public class VirtualDrive
    {
        /// <summary>
        /// Specifies the path where the drive will be located.  The drive format
        /// is indicated by the file type, either <b>.vhd</b> or <b>.vhdx</b>.
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// The drive size in bytes.
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Indicates whether a dynamic drive will be created as opposed to a
        /// pre-allocated fixed drive.  This defaults to <b>true</b>.
        /// </summary>
        public bool IsDynamic { get; set; } = true;
    }
}

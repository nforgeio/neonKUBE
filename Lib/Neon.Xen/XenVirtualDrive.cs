//-----------------------------------------------------------------------------
// FILE:	    XenVirtualDrive.cs
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

namespace Neon.Xen
{
    /// <summary>
    /// Specifies virtual drive creation parameters.
    /// </summary>
    public class XenVirtualDrive
    {
        /// <summary>
        /// The drive size in bytes.
        /// </summary>
        public long Size { get; set; }
    }
}

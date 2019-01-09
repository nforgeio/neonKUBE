//-----------------------------------------------------------------------------
// FILE:	    Win32.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Dynamic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Csv;
using Neon.IO;

namespace Neon.Windows
{
    /// <summary>
    /// Low-level Windows system calls.
    /// </summary>
    public static class Win32
    {
        /// <summary>
        /// Returns the total installed physical RAM as kilobytes.
        /// </summary>
        /// <param name="TotalMemoryInKilobytes"></param>
        /// <returns></returns>
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);
    }
}

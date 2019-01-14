//-----------------------------------------------------------------------------
// FILE:	    Win32.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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
        /// <param name="TotalMemoryInKilobytes">Returns as the physical RAM as KiB.</param>
        /// <returns><c>true</c> on success.</returns>
        [DllImport("kernel32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetPhysicallyInstalledSystemMemory(out long TotalMemoryInKilobytes);

        /// <summary>
        /// Has Windows encrypt a file or folder at rest.
        /// </summary>
        /// <param name="path">The file or folder path.</param>
        /// <returns><c>true</c> on success.</returns>
        [DllImport("advapi32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EncryptFile(string path);
    }
}

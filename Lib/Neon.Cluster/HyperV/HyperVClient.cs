//-----------------------------------------------------------------------------
// FILE:	    HyperVClient.cs
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
    /// <para>
    /// Abstracts management of local Hyper-V virtual machines and components
    /// on Windows via PowerShell.
    /// </para>
    /// <note>
    /// This class requires elevated administrative rights.
    /// </note>
    /// </summary>
    /// <threadsafety instance="false"/>
    public class HyperVClient : IDisposable
    {
        private PowerShell      powershell;

        /// <summary>
        /// Default constructor to be used to manage Hyper-V objects
        /// on the local Windows machine.
        /// </summary>
        public HyperVClient()
        {
            if (!NeonHelper.IsWindows)
            {
                throw new NotSupportedException($"{nameof(HyperVClient)} is only supporteds on Windows.");
            }

            powershell = new PowerShell();
        }

        /// <summary>
        /// Releases all resources associated with the instance.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases all associated resources.
        /// </summary>
        /// <param name="disposing">Pass <c>true</c> if we're disposing, <c>false</c> if we're finalizing.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (powershell != null)
            {
                powershell.Dispose();
            }
        }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    KubeHostPlatform.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

using Renci.SshNet;

namespace Neon.Kube
{
    /// <summary>
    /// Enumerates the operating systems supported by neonKUBE.
    /// </summary>
    public enum KubeHostPlatform
    {
        /// <summary>
        /// Linux.
        /// </summary>
        Linux,

        /// <summary>
        /// Windows.
        /// </summary>
        Windows,

        /// <summary>
        /// OS/X
        /// </summary>
        Osx
    }
}

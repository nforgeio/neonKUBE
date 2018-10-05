//-----------------------------------------------------------------------------
// FILE:	    IXenClient.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;

namespace Neon.Hive
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Used internally by neonHIVE as a potentially temporary
    /// hack required by <see cref="SetupController{NodeMetadata}"/> to display XenServer
    /// provisioning status.  This may be removed at some point in the future.
    /// </summary>
    public interface IXenClient
    {
        /// <summary>
        /// Returns the name of the connected XenServer.
        /// </summary>
        string Name { get; }
    }
}

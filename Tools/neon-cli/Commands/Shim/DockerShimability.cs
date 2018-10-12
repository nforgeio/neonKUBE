//-----------------------------------------------------------------------------
// FILE:	    DockerShimability.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Hive;

namespace NeonCli
{
    /// <summary>
    /// Enumerates the possible shimming options for a command.
    /// </summary>
    public enum DockerShimability
    {
        /// <summary>
        /// Indicates that the command cannot be shimmed.
        /// </summary>
        None,

        /// <summary>
        /// Indicates that the command may be shimmed but that shimming is not required.
        /// </summary>
        Optional,

        /// <summary>
        /// Indicates the command must be shimmed.
        /// </summary>
        Required
    }
}

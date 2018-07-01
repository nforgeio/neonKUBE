//-----------------------------------------------------------------------------
// FILE:	    DockerShimInfo.cs
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
    /// Information returned by the <see cref="ICommand.Shim(DockerShim)"/> methods
    /// with information about whether and how to shim a command to the <b>neon-cli</b>
    /// container.
    /// </summary>
    public class DockerShimInfo
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="isShimmed">Indicates whether the command needs to be shimmed.</param>
        /// <param name="ensureConnection">
        /// Indicates that the command requires a hive login and VPN connection
        /// (if enabled) before the command is executed in a <b>neon-cli</b>
        /// container.  This defaults to <c>false</c>.
        /// </param>
        public DockerShimInfo(bool isShimmed, bool ensureConnection = false)
        {
            this.IsShimmed        = isShimmed;
            this.EnsureConnection = ensureConnection;
        }

        /// <summary>
        /// Indicates whether the command needs to be shimmed.
        /// </summary>
        public bool IsShimmed { get; set; }

        /// <summary>
        /// Indicates that the command requires a hive login and VPN connection
        /// (if enabled) before the command is executed in a <b>neon-cli</b>
        /// container.
        /// </summary>
        public bool EnsureConnection { get; set; }
    }
}

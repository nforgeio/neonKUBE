//-----------------------------------------------------------------------------
// FILE:	    RunOptions.cs
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

using Renci.SshNet;

namespace Neon.Hive
{
    /// <summary>
    /// Enumerates the possible options to use when executing a remote
    /// command on a <see cref="SshProxy{T}"/>.  These options may be 
    /// combined using the bitwise OR operator.
    /// </summary>
    [Flags]
    public enum RunOptions
    {
        /// <summary>
        /// No options are set.
        /// </summary>
        None = 0x00000000,

        /// <summary>
        /// Bitwise ORs any specific option flags with those specified by <see cref="SshProxy{TMetadata}.DefaultRunOptions"/>.
        /// This is handy for setting or resetting flags like <see cref="FaultOnError"/> on a global 
        /// basis for a node proxy instance.
        /// </summary>
        Defaults = 0x00000001,

        /// <summary>
        /// Puts the <see cref="SshProxy{T}"/> into the faulted state when the command
        /// returns a non-zero exit code.
        /// </summary>
        FaultOnError = 0x00000002,

        /// <summary>
        /// Runs the command even if the <see cref="SshProxy{T}"/> is in the faulted state.
        /// </summary>
        RunWhenFaulted = 0x00000004,

        /// <summary>
        /// Ignore the <see cref="SshProxy{TMetadata}.RemotePath"/> property.
        /// </summary>
        IgnoreRemotePath = 0x00000008,

        /// <summary>
        /// Return the standard output from remote command as binary data rather
        /// than intrepreting it as text.
        /// </summary>
        BinaryOutput = 0x00000010,

        /// <summary>
        /// Use for commands that may include sensitive secrets as command arguments
        /// and/or results.  Only limited information about commands run with this
        /// flag will be logged.
        /// </summary>
        Redact = 0x00000020,

        /// <summary>
        /// Logs command output only if the command returns a non-zero exit code.
        /// </summary>
        LogOnErrorOnly = 0x00000040,

        /// <summary>
        /// Logs the command standard output (standard error output is logged by default).
        /// </summary>
        LogOutput = 0x00000080,

        /// <summary>
        /// Used internally to prevent logging of the command "START: *" line at 
        /// lower levels because this has already been logged.
        /// </summary>
        LogBundle = 0x00000100,

        /// <summary>
        /// Used to mark commands whose execution should be logged for auditing.
        /// </summary>
        Audit = 0x00000200,

        /// <summary>
        /// <para>
        /// Indicates that the command will shutdown or restart or reboot
        /// the target server.  Commands with this flag will not be retried
        /// on the server.
        /// </para>
        /// <note>
        /// IMPORTANT: You must specify this flag if your command shutdown
        /// down the machine to prevent causing an infinite reboot loop.
        /// </note>
        /// </summary>
        Shutdown = 0x00000400
    }
}

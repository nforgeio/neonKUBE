//-----------------------------------------------------------------------------
// FILE:	    CommandResponse.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Net;
using Neon.Retry;
using Neon.Time;

using ICSharpCode.SharpZipLib.Zip;

using Renci.SshNet;
using Renci.SshNet.Common;
using System.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Describes the results of a command executed on the remote server using
    /// <see cref="NodeProxy{TMetadata}.RunCommand(CommandBundle, RunOptions)"/> 
    /// or <see cref="NodeProxy{TMetadata}.SudoCommand(string, object[])"/>.
    /// </summary>
    public class CommandResponse
    {
        /// <summary>
        /// Returns the original command line.
        /// </summary>
        public string Command { get; internal set; }

        /// <summary>
        /// Returns the command nicely formatted across multiple lines of text
        /// that is suitable for including in a Bash script.
        /// </summary>
        public string BashCommand { get; internal set; }

        /// <summary>
        /// Returns the command exit code.
        /// </summary>
        public int ExitCode { get; internal set; }

        /// <summary>
        /// Returns <c>true</c> if the command exit code was zero, 
        /// <b>false</b> otherwise.
        /// </summary>
        public bool Success
        {
            get { return ExitCode == 0; }
        }

        /// <summary>
        /// Indicates whether the command failed because the proxy is faulted due to a previous error.
        /// </summary>
        public bool ProxyIsFaulted { get; internal set; }

        /// <summary>
        /// Returns the command standard output as a string when <see cref="RunOptions.BinaryOutput"/> 
        /// is not specified.
        /// </summary>
        public string OutputText { get; internal set; } = string.Empty;

        /// <summary>
        /// Creates a <see cref="TextReader"/> over the command's standard output result.
        /// </summary>
        public TextReader OpenOutputTextReader()
        {
            return new StringReader(OutputText);
        }

        /// <summary>
        /// Returns the command standard error as a string.
        /// </summary>
        public string ErrorText { get; internal set; } = string.Empty;

        /// <summary>
        /// Creates a <see cref="TextReader"/> over the command's standard error result.
        /// </summary>
        public TextReader OpenErrorTextReader()
        {
            return new StringReader(ErrorText);
        }

        /// <summary>
        /// Returns the command standard output and error as a string.
        /// </summary>
        public string AllText
        {
            get { return OutputText + ErrorText; }
        }

        /// <summary>
        /// Creates a <see cref="TextReader"/> over the command's standard output and standard error results.
        /// </summary>
        public TextReader OpenAllTextReader()
        {
            return new StringReader(AllText);
        }

        /// <summary>
        /// Returns the command standard output as a byte array when <see cref="RunOptions.BinaryOutput"/> 
        /// is specified.
        /// </summary>
        public byte[] OutputBinary { get; internal set; } = null;

        /// <summary>
        /// Creates a <see cref="Stream"/> that can be used to read the standard output as binary when 
        /// <see cref="RunOptions.BinaryOutput"/> is specified.
        /// </summary>
        /// <returns>The <see cref="Stream"/>.</returns>
        public Stream OpenOutputBinaryStream()
        {
            return new MemoryStream(OutputBinary ?? new byte[0]);
        }

        /// <summary>
        /// Returns a brief message suitable for including in a related exception message.
        /// </summary>
        public string ErrorSummary
        {
            get
            {
                if (Success)
                {
                    return string.Empty;
                }

                return $"[exitcode={ExitCode}: {Command}";
            }
        }
    }
}

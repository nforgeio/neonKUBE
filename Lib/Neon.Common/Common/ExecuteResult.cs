//-----------------------------------------------------------------------------
// FILE:        ExecuteResult.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by NeonForge, LLC.  All rights reserved.

using System;
using System.Diagnostics;

namespace Neon.Common
{
    /// <summary>
    /// Holds the process exit code and captured standard output from a process
    /// launched by <see cref="NeonHelper.ExecuteCaptureStreams(string, string, TimeSpan?, Process)"/>.
    /// </summary>
    public class ExecuteResult
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        internal ExecuteResult()
        {
        }

        /// <summary>
        /// Returns the process exit code.
        /// </summary>
        public int ExitCode { get; internal set; }

        /// <summary>
        /// Returns the captured standard output stream text from the process.
        /// </summary>
        public string OutputText { get; internal set; }

        /// <summary>
        /// Returns the captured standard error stream text from the process.
        /// </summary>
        public string ErrorText { get; internal set; }

        /// <summary>
        /// Returns trhe captured standard output and error stream text from the process.
        /// </summary>
        public string AllText
        {
            get { return OutputText + ErrorText; }
        }
    }
}

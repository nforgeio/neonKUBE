//-----------------------------------------------------------------------------
// FILE:        ExecuteResult.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Diagnostics;

namespace Neon.Common
{
    /// <summary>
    /// Holds the process exit code and captured standard output from a process
    /// launched by <see cref="NeonHelper.ExecuteCapture(string, string, TimeSpan?, Process, Action{string}, Action{string})"/>.
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

        /// <summary>
        /// Ensure that the command returned a zero exit code.
        /// </summary>
        /// <exception cref="ExecuteException">Thrown if the exit code isn't zero.</exception>
        public void EnsureSuccess()
        {
            if (ExitCode != 0)
            {
                // We're going to use the error text as the exception message if this
                // isn't empty, otherwise we'll use the output text.

                if (ErrorText.Trim().Length > 0)
                {
                    throw new ExecuteException(ExitCode, ErrorText);
                }
                else
                {
                    throw new ExecuteException(ExitCode, OutputText);
                }
            }
        }
    }
}

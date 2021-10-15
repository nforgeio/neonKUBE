//-----------------------------------------------------------------------------
// FILE:        ExecuteResponse.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Neon.Common
{
    /// <summary>
    /// Holds the process exit code and captured standard output from a process launched by any of the
    /// <see cref="NeonHelper.ExecuteCapture(string, object[], TimeSpan?, Process, string, System.Collections.Generic.Dictionary{string, string}, Action{string}, Action{string}, TextReader, Encoding)"/>
    /// related methods.
    /// </summary>
    public class ExecuteResponse
    {
        private string      cachedAllText = null;

        /// <summary>
        /// Internal constructor.
        /// </summary>
        internal ExecuteResponse()
        {
        }

        /// <summary>
        /// Constuctor.
        /// </summary>
        /// <param name="exitCode">The exit code.</param>
        /// <param name="outputText">Optionally specifies the output text.</param>
        /// <param name="errorText">Optionally specifies the error text.</param>
        public ExecuteResponse(int exitCode, string outputText = null, string errorText = null)
        {
            this.ExitCode   = exitCode;
            this.OutputText = outputText ?? string.Empty;
            this.ErrorText  = errorText ?? string.Empty;
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
        /// Returns the captured standard output and error stream text from the process.
        /// </summary>
        public string AllText
        {
            get
            {
                if (cachedAllText != null)
                {
                    return cachedAllText;
                }

                return cachedAllText = OutputText + ErrorText;
            }
        }

        /// <summary>
        /// Ensure that the command returned a zero exit code.
        /// </summary>
        /// <returns>The response for fluent-style chaining.</returns>
        /// <exception cref="ExecuteException">Thrown if the exit code isn't zero.</exception>
        public ExecuteResponse EnsureSuccess()
        {
            if (ExitCode != 0)
            {
                // We're going to use the error text as the exception message if this
                // isn't empty, otherwise we'll use the output text.

                if (ErrorText.Trim().Length > 0)
                {
                    throw new ExecuteException(this, ErrorText);
                }
                else
                {
                    throw new ExecuteException(this, OutputText);
                }
            }

            return this;
        }
    }
}

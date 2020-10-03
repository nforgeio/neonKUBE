//-----------------------------------------------------------------------------
// FILE:	    CommandResponse.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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

namespace Neon.SSH
{
    /// <summary>
    /// Describes the results of a command executed on the remote server using
    /// <see cref="SshLinuxProxy{TMetadata}.RunCommand(CommandBundle, RunOptions)"/> 
    /// or <see cref="SshLinuxProxy{TMetadata}.SudoCommand(string, object[])"/>.
    /// </summary>
    public class CommandResponse : IBashCommandFormatter
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
        public bool Success => ExitCode == 0;

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
        /// <returns>The <see cref="TextReader"/>.</returns>
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
        /// <returns>The <see cref="TextReader"/>.</returns>
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
        /// <returns>The <see cref="TextReader"/>.</returns>
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
            return new MemoryStream(OutputBinary ?? Array.Empty<byte>());
        }

        /// <summary>
        /// Returns an error message suitable for including in a related exception message.
        /// </summary>
        public string ErrorSummary
        {
            get
            {
                if (Success)
                {
                    return string.Empty;
                }

                return $"[exitcode={ExitCode}]: {ErrorText}";
            }
        }

        /// <summary>
        /// Converts the original command into a Bash command.
        /// </summary>
        /// <param name="comment">Optionall specifies a comment string to be included.</param>
        /// <returns>The Bash command string.</returns>
        public string ToBash(string comment = null)
        {
            if (string.IsNullOrEmpty(comment))
            {
                return BashCommand;
            }
            else
            {
                var sb = new StringBuilder();

                sb.AppendLine($"# {comment}");
                sb.AppendLine();
                sb.Append(BashCommand);

                return sb.ToString();
            }
        }

        /// <summary>
        /// Ensures that the response has a zero exit code.
        /// </summary>
        /// <exception cref="ExecuteException">Thrown if when <see cref="ExitCode"/> is non-zero.</exception>
        public void EnsureSuccess()
        {
            if (!Success)
            {
                throw new ExecuteException(ExitCode, ErrorSummary);
            }
        }
    }
}

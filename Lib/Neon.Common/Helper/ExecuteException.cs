//-----------------------------------------------------------------------------
// FILE:	    ExecuteException.cs
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
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Diagnostics;

namespace Neon.Common
{
    /// <summary>
    /// Thrown by <see cref="ExecuteResponse.EnsureSuccess"/> if the executed command
    /// did not return a <b>zero</b> exit code.
    /// </summary>
    public class ExecuteException : Exception
    {
        /// <summary>
        /// Constructs an instance from a <see cref="ExecuteResponse"/>.
        /// </summary>
        /// <param name="response">The command response.</param>
        /// <param name="message">The error message.</param>
        public ExecuteException(ExecuteResponse response, string message)
            : base(message)
        {
            Covenant.Requires<ArgumentNullException>(response != null, nameof(response));

            this.ExitCode   = response.ExitCode;
            this.OutputText = response.OutputText;
            this.ErrorText  = response.ErrorText;
        }

        /// <summary>
        /// Constructs an instance explictly passing the exit code and output streams.
        /// </summary>
        /// <param name="exitCode">The program exit code.</param>
        /// <param name="message">The error message.</param>
        /// <param name="outputText">The program standard output text.</param>
        /// <param name="errorText">The program standard error text.</param>
        public ExecuteException(int exitCode, string message, string outputText, string errorText)
            : base(message)
        {
            this.ExitCode   = exitCode;
            this.OutputText = outputText ?? string.Empty;
            this.ErrorText  = errorText ?? string.Empty;
        }

        /// <summary>
        /// Returns the command exit code.
        /// </summary>
        public int ExitCode { get; private set; }

        /// <summary>
        /// Returns the command standard output text.
        /// </summary>
        public string OutputText { get; private set; }

        /// <summary>
        /// Returns the command standard error text.
        /// </summary>
        public string ErrorText { get; private set; }
    }
}

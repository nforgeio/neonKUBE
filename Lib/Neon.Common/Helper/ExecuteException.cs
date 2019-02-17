//-----------------------------------------------------------------------------
// FILE:	    ExecuteException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
        /// Constructor.
        /// </summary>
        /// <param name="exitCode">The command exit code.</param>
        /// <param name="message">The error message.</param>
        public ExecuteException(int exitCode, string message)
            : base(message)
        {
            this.ExitCode = exitCode;
        }

        /// <summary>
        /// Returns the command exit code.
        /// </summary>
        public int ExitCode { get; private set; }
    }
}

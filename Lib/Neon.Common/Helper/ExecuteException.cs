//-----------------------------------------------------------------------------
// FILE:	    ExecuteException.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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
    /// Thrown by <see cref="ExecuteResult.EnsureSuccess"/> if the executed command
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

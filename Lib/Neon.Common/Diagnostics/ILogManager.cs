//-----------------------------------------------------------------------------
// FILE:	    ILogManager.cs
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
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace Neon.Diagnostics
{
    /// <summary>
    /// Describes Log Manager implementations.
    /// </summary>
    public interface ILogManager : ILoggerProvider
    {
        /// <summary>
        /// Specifies the level of events to be actually recorded.
        /// </summary>
        LogLevel LogLevel { get; set; }

        /// <summary>
        /// Sets the log level by safely parsing a string.
        /// </summary>
        /// <param name="level">The level string or <c>null</c>.</param>
        /// <remarks>
        /// <para>
        /// This method recognizes the following case insensitive values: <b>CRITICAL</b>,
        /// <b>SERROR</b>, <b>ERROR</b>, <b>WARN</b>, <b>WARNING</b>, <b>INFO</b>, <b>SINFO</b>,
        /// <b>INFORMATION</b>, <b>DEBUG</b>, or <b>NONE</b>.
        /// </para>
        /// <note>
        /// <b>INFO</b> will be assumed if the parameter doesn't match any of the
        /// values listed above.
        /// </note>
        /// </remarks>
        void SetLogLevel(string level);

        /// <summary>
        /// Controls whether timestamps are emitted.  This defaults to <c>true</c>.
        /// </summary>
        bool EmitTimestamp { get; set; }

        /// <summary>
        /// Controls whether the <b>index</b> field is emitted.  This is a counter start
        /// starts at zero for each application instance and is incremented for each event 
        /// emitted to help reconstruct exactly what happened when the system time resolution
        /// isn't fine enough.  This defaults to <c>true</c>.
        /// </summary>
        bool EmitIndex { get; set; }

        /// <summary>
        /// Returns a named logger.
        /// </summary>
        /// <param name="sourceModule">The case sensitive logger event source module (defaults to <c>null</c>).</param>
        /// <returns>The <see cref="INeonLogger"/> instance.</returns>
        INeonLogger GetLogger(string sourceModule = null);

        /// <summary>
        /// Returns a logger to be associated with a specific type.  This method
        /// supports both <c>static</c> and normal types.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The <see cref="INeonLogger"/> instance.</returns>
        INeonLogger GetLogger(Type type);

        /// <summary>
        /// Returns a logger to be associated with a specific type.  This
        /// method works only for non-<c>static</c> types.
        /// </summary>
        /// <typeparam name="T">The type.</typeparam>
        /// <returns>The <see cref="INeonLogger"/> instance.</returns>
        INeonLogger GetLogger<T>();
    }
}

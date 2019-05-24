//-----------------------------------------------------------------------------
// FILE:	    NonRetriableErrors.cs
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
using System.ComponentModel;
using System.Diagnostics;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Used in conjunction with <see cref="CadenceRetryPolicy"/> to specify errors that
    /// <b>will not</b> cause a workflow related operation to be retried.
    /// </summary>
    public static class NonRetriableErrors
    {
        /// <summary>
        /// Returns the non-retriable error string for a <b>custom error</b>.
        /// </summary>
        /// <param name="reason">The reason string.</param>
        public static string Custom(string reason)
        {
            return reason;
        }

        /// <summary>
        /// Returns the non-retriable error string for a <b>panic error</b>.
        /// </summary>
        public static string Panic()
        {
            return "cadenceInternal:Panic";
        }

        /// <summary>
        /// Returns the non-retriable error string for a <b>generic error</b>.
        /// </summary>
        public static string Generic()
        {
            return "cadenceInternal:Generic";
        }

        /// <summary>
        /// Returns the non-retriable error string for a <b>start-to-close timeout</b>.
        /// </summary>
        public static string StartToCloseTimeout()
        {
            return "cadenceInternal:Timeout START_TO_CLOSE";
        }

        /// <summary>
        /// Returns the non-retriable error string for a <b>heartbeat timeout</b>.
        /// </summary>
        public static string HeartbeatTimeout()
        {
            return "cadenceInternal:Timeout HEARTBEAT";
        }
    }
}

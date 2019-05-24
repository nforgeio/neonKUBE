//-----------------------------------------------------------------------------
// FILE:	    WorkflowResetPoint.cs
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

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// <para>
    /// Not sure what is for.
    /// </para>
    /// <note>
    /// I'm making this <c>internal</c> for now until we decide it makes sense
    /// to expose this to .NET workflow applications.
    /// </note>
    /// </summary>
    internal class WorkflowResetPoint
    {
        /// <summary>
        /// Not sure what is.
        /// </summary>
        public string BinaryChecksum { get; internal set; }

        /// <summary>
        /// Not sure what is.
        /// </summary>
        public string RunId { get; internal set; }

        /// <summary>
        /// Not sure what is.
        /// </summary>
        public long FirstDecisionCompletedId { get; internal set; }

        /// <summary>
        /// Not sure what is.
        /// </summary>
        public DateTime CreatedTime { get; internal set; }

        /// <summary>
        /// Not sure what is.
        /// </summary>
        public DateTime ExpiringTime { get; internal set; }

        /// <summary>
        /// Not sure what is.
        /// </summary>
        public bool Resettable { get; internal set; }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    SyncSignalInfo.cs
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
using System.ComponentModel;

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Holds information necessary to implement synchronous signals.  This is
    /// added internally as the first parameter to synchronous signals by the
    /// <see cref="CadenceClient"/>.
    /// </summary>
    internal class SyncSignalInfo
    {
        /// <summary>
        /// The signal name used for synchronous signals.  Signals sent here will be
        /// handled internally by <see cref="WorkflowBase"/> and forwarded on to the
        /// user's signal handler method.
        /// </summary>
        public const string SyncSignalName = "__cadence-sync-signal";

        /// <summary>
        /// Identifies the signal method targeted by the user.  We need this because the 
        /// the signal will be sent to <see cref="SyncSignalName"/> and the internal handler
        /// will need this to identify the actual user single method.
        /// </summary>
        [JsonProperty(PropertyName = "TargetSignal", Required = Required.Always)]
        public string TargetSignal { get; set; }

        /// <summary>
        /// Specifies where the workflow will send the <see cref="SyncSignalReply"/> when
        /// the signal completes.  This URI targets the calling client and includes enough
        /// information so the client can map the reply to the waiting operation.
        /// </summary>
        [JsonProperty(PropertyName = "ReplyUri", Required = Required.Always)]
        public string ReplyUri { get; set; }
    }
}

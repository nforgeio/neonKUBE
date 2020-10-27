//-----------------------------------------------------------------------------
// FILE:	    SyncSignalCall.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;

using Newtonsoft.Json;

using Neon.Common;
using Neon.Temporal;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// <b>INTERNAL USE ONLY:</b> Holds information necessary to implement synchronous 
    /// signals.  This is used internally for transmitting synchronous signals 
    /// to workflows.
    /// </summary>
    public class SyncSignalCall
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="targetSignal">Identifies the target signal.</param>
        /// <param name="signalId">The globally unique signal ID.</param>
        /// <param name="userArgs">The encoded user arguments being passed to the signal.</param>
        public SyncSignalCall(string targetSignal, string signalId, byte[] userArgs)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(targetSignal), nameof(targetSignal));
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(signalId), nameof(signalId));
            Covenant.Requires<ArgumentNullException>(userArgs != null, nameof(userArgs));

            this.TargetSignal = targetSignal;
            this.SignalId     = signalId;
            this.UserArgs     = userArgs;
        }

        /// <summary>
        /// Identifies the signal method targeted by the user.  We need this because the 
        /// the signal will be sent to <see cref="TargetSignal"/> and the internal handler
        /// will need this to identify the actual user single method to be called.
        /// </summary>
        [JsonProperty(PropertyName = "TargetSignal", Required = Required.Always)]
        public string TargetSignal { get; set; }

        /// <summary>
        /// Specifies a globally unique ID for the signal request operation.  The
        /// target worker will manage the current state of the signal request and
        /// the client will use this to poll the worker for the current state.
        /// </summary>
        [JsonProperty(PropertyName = "SignalId", Required = Required.Always)]
        public string SignalId { get; set; }

        /// <summary>
        /// The encoded user arguments being passed to the signal.
        /// </summary>
        [JsonProperty(PropertyName = "UserArgs", Required = Required.Always)]
        public byte[] UserArgs { get; set; }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    SyncSignalReply.cs
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

using Neon.Temporal;
using Neon.Common;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// Holds the internal reply for synchronous signals.
    /// </summary>
    internal class SyncSignalReply
    {
        /// <summary>
        /// Used to indicate that an exception was thrown by the signal method.
        /// This will be set to the exception name and error message.  This will
        /// be <c>null</c> when the signal method completed successfully.
        /// </summary>
        [JsonProperty(PropertyName = "Error", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public bool Error { get; set; }

        /// <summary>
        /// This holds the result for signals that return result and will be
        /// <c>null</c> for signals that don't return a result.
        /// </summary>
        [JsonProperty(PropertyName = "Result", Required = Required.Default, DefaultValueHandling = DefaultValueHandling.IgnoreAndPopulate)]
        [DefaultValue(null)]
        public object Result { get; set; }
    }
}

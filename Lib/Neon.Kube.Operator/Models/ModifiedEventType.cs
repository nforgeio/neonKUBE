//-----------------------------------------------------------------------------
// FILE:	    ModifiedEventType.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Kube.Operator
{
    /// <summary>
    /// Enumerates different event types. Kubernetes combines all of these events into the 
    /// <see cref="k8s.WatchEventType.Modified"/> event, but we need to handle them each differently
    /// in the operator.
    /// </summary>
    public enum ModifiedEventType
    {
        /// <summary>
        /// When the modified event is anything other than the special cases.
        /// </summary>
        [EnumMember(Value = "other")]
        Other,

        /// <summary>
        /// Modified event should run finalizers.
        /// </summary>
        [EnumMember(Value = "finalizing")]
        Finalizing,

        /// <summary>
        /// Represents no changes.
        /// </summary>
        [EnumMember(Value = "no-changes")]
        NoChanges,

        /// <summary>
        /// Represents a finalizer update event.
        /// </summary>
        [EnumMember(Value = "finalizer-update")]
        FinalizerUpdate,

        /// <summary>
        /// Represents a status update event.
        /// </summary>
        [EnumMember(Value = "status-update")]
        StatusUpdate
    }
}

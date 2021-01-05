//-----------------------------------------------------------------------------
// FILE:	    InternalRegisterNamespaceRequest.cs
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
using System.ComponentModel;

using Neon.Common;
using Neon.Temporal;

namespace Neon.Temporal.Internal
{
    /// <summary>
    /// Namespace registration details.
    /// </summary>
    internal class InternalRegisterNamespaceRequest
    {
        /// <summary>
        /// The namespace name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The namespace description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The namespace owner's email address.
        /// </summary>
        public string OwnerEmail { get; set; }

        /// <summary>
        /// The number of days to retain the history for workflowws
        /// completed in this namespace.  This defaults to <b>7 days</b>.
        /// </summary>
        public int RetentionDays { get; set; } = 7;

        /// <summary>
        /// Enables metric generation.  This defaults to <c>false.</c>
        /// </summary>
        public bool EmitMetrics { get; set; }

        /// <summary>
        /// Optional security token.
        /// </summary>
        public string SecurityToken { get; set; }
    }
}

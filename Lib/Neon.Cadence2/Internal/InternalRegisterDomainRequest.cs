//-----------------------------------------------------------------------------
// FILE:	    InternalRegisterDomainRequest.cs
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
using Neon.Common;

namespace Neon.Cadence.Internal
{
    /// <summary>
    /// Domain registration details.
    /// </summary>
    internal class InternalRegisterDomainRequest
    {
        /// <summary>
        /// The domain name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The domain description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The domain owner's email address.
        /// </summary>
        public string OwnerEmail { get; set; }

        /// <summary>
        /// The number of days to retain the history for workflowws
        /// completed in this domain.  This defaults to <b>7 days</b>.
        /// </summary>
        public int RetentionDays { get; set; } = 7;

        /// <summary>
        /// Enables metric generation.  This defaults to <c>false.</c>
        /// </summary>
        public bool EmitMetrics { get; set; }
    }
}

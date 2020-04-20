//-----------------------------------------------------------------------------
// FILE:	    DomainInfo.cs
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

using Neon.Common;
using Neon.Temporal;
using Neon.Temporal.Internal;

namespace Neon.Temporal
{
    /// <summary>
    /// Information about a Temporal namespace.
    /// </summary>
    public class DomainInfo
    {
        /// <summary>
        /// The namespace name.
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The namespace status.
        /// </summary>
        public DomainStatus Status { get; set; }

        /// <summary>
        /// Ths namespace description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// The namespace owner's email address.
        /// </summary>
        public string OwnerEmail { get; set; }
    }
}

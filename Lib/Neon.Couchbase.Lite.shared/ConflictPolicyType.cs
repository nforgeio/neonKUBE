//-----------------------------------------------------------------------------
// FILE:	    ConflictPolicyType.cs
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.DynamicData;

namespace Couchbase.Lite
{
    /// <summary>
    /// Enumerates the basic types of conflict policies.
    /// </summary>
    public enum ConflictPolicyType
    {
        /// <summary>
        /// Conflicts will be resolved by a custom policy derived from <see cref="CustomConflictPolicy"/>.
        /// </summary>
        Custom,

        /// <summary>
        /// Conflicts will cause the document save operation to fail.
        /// </summary>
        Fail,

        /// <summary>
        /// Conflicts will be persisted to the database and will be otherwise ignored.
        /// </summary>
        Ignore,

        /// <summary>
        /// Conflicts will be resolved by keeping the revision already saved to the database.
        /// </summary>
        KeepOther,

        /// <summary>
        /// Conflicts will be resolved in favor of the revision being saved.
        /// </summary>
        KeepThis
    }
}

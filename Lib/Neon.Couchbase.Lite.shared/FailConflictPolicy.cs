//-----------------------------------------------------------------------------
// FILE:	    FailConflictPolicy.cs
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
    /// This policy throws a <see cref="ConflictException"/> when a document cannot 
    /// be persisted due to a conflict.
    /// </summary>
    public sealed class FailConflictPolicy : ConflictPolicy
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        internal FailConflictPolicy()
        {
        }
        
        /// <inheritdoc/>
        public override ConflictPolicyType Type
        {
            get { return ConflictPolicyType.Fail; }
        }

        /// <inheritdoc/>
        public override void Resolve(ConflictDetails details)
        {
            throw new ConflictException($"[{nameof(FailConflictPolicy)}]: Failed to resolve a [{details.EntityDocument.EntityType.FullName}] conflict.");
        }
    }
}

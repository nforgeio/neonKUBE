//-----------------------------------------------------------------------------
// FILE:	    KeepThisConflictPolicy.cs
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
    /// This policy resolves conflicts by overwriting the existing revision in the
    /// database with the one being saved.
    /// </summary>
    public sealed class KeepThisConflictPolicy : ConflictPolicy
    {
        /// <summary>
        /// Internal constructor.
        /// </summary>
        internal KeepThisConflictPolicy()
        {
        }

        /// <inheritdoc/>
        public override ConflictPolicyType Type
        {
            get { return ConflictPolicyType.KeepThis; }
        }

        /// <inheritdoc/>
        public override void Resolve(ConflictDetails details)
        {
            // Delete all conflicting revisions except for the current one.

            foreach (var conflict in details.ConflictingRevisions)
            {
                if (conflict == details.Document.CurrentRevision)
                {
                    continue;
                }

                var unsavedConflict = conflict.CreateRevision();

                unsavedConflict.IsDeletion = true;
                unsavedConflict.Save();
            }

            // ...and then save this one.

            var unsavedRevision = details.Document.CreateRevision();

            unsavedRevision.SetProperties(details.EntityDocument.Properties);
            details.SavedRevision = unsavedRevision.Save();
        }
    }
}

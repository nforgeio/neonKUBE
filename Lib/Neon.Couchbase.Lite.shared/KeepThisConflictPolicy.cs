//-----------------------------------------------------------------------------
// FILE:	    KeepThisConflictPolicy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

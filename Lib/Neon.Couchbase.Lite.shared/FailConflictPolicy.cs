//-----------------------------------------------------------------------------
// FILE:	    FailConflictPolicy.cs
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

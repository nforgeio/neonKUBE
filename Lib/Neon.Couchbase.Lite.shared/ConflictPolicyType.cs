//-----------------------------------------------------------------------------
// FILE:	    ConflictPolicyType.cs
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

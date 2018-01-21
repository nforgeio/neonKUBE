//-----------------------------------------------------------------------------
// FILE:	    CustomConflictPolicy.cs
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
    /// Base class for custom application-defined conflict resolution policies.
    /// </summary>
    public abstract class CustomConflictPolicy : ConflictPolicy
    {
        /// <summary>
        /// Protected constructor.
        /// </summary>
        protected CustomConflictPolicy()
        {
        }

        /// <inheritdoc/>
        public override ConflictPolicyType Type
        {
            get { return ConflictPolicyType.Custom; }
        }
    }
}

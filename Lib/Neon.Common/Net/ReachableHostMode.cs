//-----------------------------------------------------------------------------
// FILE:	    ReachableHostMode.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Net
{
    /// <summary>
    /// Enumerates how <see cref="NetHelper.GetReachableHost(IEnumerable{string}, ReachableHostMode)"/> should
    /// behave when no there are no healthy hosts.
    /// </summary>
    public enum ReachableHostMode
    {
        /// <summary>
        /// Throw an exception when no hosts respond.
        /// </summary>
        Throw,

        /// <summary>
        /// Return the first host when no hosts respond.
        /// </summary>
        ReturnFirst,

        /// <summary>
        /// Return <c>null</c> when no hosts respond.
        /// </summary>
        ReturnNull
    }
}

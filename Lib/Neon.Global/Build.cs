//-----------------------------------------------------------------------------
// FILE:	    Build.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon
{
    /// <summary>
    /// Neon build constants.
    /// </summary>
    internal static partial class Build
    {
        /// <summary>
        /// The company name to use for all Neon assemblies.
        /// </summary>
        public const string Company = "neonFORGE, LLC";

        /// <summary>
        /// The copyright statement to be included in all assemblies.
        /// </summary>
        public const string Copyright = "Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.";

        /// <summary>
        /// The product statement to be included in all assemblies.
        /// </summary>
        public const string Product = "ksetup";

        /// <summary>
        /// The build configuration.
        /// </summary>
        public const string Configuration =
#if DEBUG
            "Debug";
#else
            "Release";
#endif
    }
}

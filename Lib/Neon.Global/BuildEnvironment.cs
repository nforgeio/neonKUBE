//-----------------------------------------------------------------------------
// FILE:	    BuildEnvironment.cs
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
    /// Describes the build environment. 
    /// </summary>
    internal static class BuildEnvironment
    {
        /// <summary>
        /// Returns the build machine name.
        /// </summary>
        public static string BuildMachine
        {
            get
            {
                return Environment.GetEnvironmentVariable("COMPUTERNAME");
            }
        }

        /// <summary>
        /// Returns the fully qualified path to the build root folder.
        /// </summary>
        public static string BuildRootPath
        {
            get
            {
                return Environment.GetEnvironmentVariable("NF_ROOT");
            }
        }

        /// <summary>
        /// Returns the fully qualified path to the build artifacts folder.
        /// </summary>
        public static string BuildArtifactPath
        {
            get
            {
                return Environment.GetEnvironmentVariable("NF_BUILD");
            }
        }
    }
}

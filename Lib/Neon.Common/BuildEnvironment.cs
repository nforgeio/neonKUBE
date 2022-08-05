//-----------------------------------------------------------------------------
// FILE:	    BuildEnvironment.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
// limitations under the License.

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
                return Environment.GetEnvironmentVariable("NK_ROOT");
            }
        }

        /// <summary>
        /// Returns the fully qualified path to the build artifacts folder.
        /// </summary>
        public static string BuildArtifactPath
        {
            get
            {
                return Environment.GetEnvironmentVariable("NK_BUILD");
            }
        }
    }
}

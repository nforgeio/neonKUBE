//-----------------------------------------------------------------------------
// FILE:	    Build.cs
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
// limitations under the License.

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
    public static partial class Build
    {
        /// <summary>
        /// The company name to use for all Neon assemblies.
        /// </summary>
        public const string Company = "neonFORGE, LLC";

        /// <summary>
        /// The copyright statement to be included in all assemblies.
        /// </summary>
        public const string Copyright = "Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.";

        /// <summary>
        /// The product name.
        /// </summary>
        public const string ProductName = "neonKUBE";

        /// <summary>
        /// The released product version.
        /// </summary>
        public const string ProductVersion = "0.3.2-alpha";

        /// <summary>
        /// The product license.
        /// </summary>
        public const string ProductLicense = "Apache License, Version 2.0";

        /// <summary>
        /// The product license URL.
        /// </summary>
        public const string ProductLicenseUrl = "http://www.apache.org/licenses/LICENSE-2.0";

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

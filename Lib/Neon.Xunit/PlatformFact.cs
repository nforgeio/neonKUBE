//-----------------------------------------------------------------------------
// FILE:        PlatformFactAttribute.cs
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;

using Xunit;

namespace Neon.Xunit
{
    /// <summary>
    /// Inherits from <see cref="FactAttribute"/> and sets <see cref="FactAttribute.Skip"/> when
    /// the <b>NEON_SKIPSLOWTESTS</b> environment variable is set to "1".
    /// </summary>
    public class PlatformFactAttribute : FactAttribute
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        /// <param name="platforms">Specifies the platforms where the tagged test should execute.</param>
        public PlatformFactAttribute(TargetPlatforms platforms)
            : base()
        {
            if (NeonHelper.IsWindows && (platforms | TargetPlatforms.Windows) != 0)
            {
                return;
            }

            if (NeonHelper.IsLinux && (platforms | TargetPlatforms.Linux) != 0)
            {
                return;
            }

            if (NeonHelper.IsOSX && (platforms | TargetPlatforms.Osx) != 0)
            {
                return;
            }

            Skip = $"Unit test is disabled for: {RuntimeInformation.OSDescription}";
        }
    }
}

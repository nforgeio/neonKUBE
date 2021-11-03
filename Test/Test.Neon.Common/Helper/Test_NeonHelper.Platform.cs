//-----------------------------------------------------------------------------
// FILE:	    Test_NeonHelper.Platform.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public partial class Test_NeonHelper
    {
        [Fact]
        public void WindowsPlatform()
        {
            // Ensure that detecting the current Windows edition doesn't barf.

            if (!NeonHelper.IsWindows)
            {
                return;     // This test works only on Windows.
            }

            Assert.NotEqual(WindowsEdition.Unknown, NeonHelper.WindowsEdition);
        }

        [Fact]
        public void GetWindowsOptionalFeatures()
        {
            var features = NeonHelper.GetWindowsOptionalFeatures();

            Assert.NotEmpty(features);
            Assert.True(features.ContainsKey("Microsoft-Hyper-V-Hypervisor"));

            var status = NeonHelper.GetWindowsOptionalFeatureStatus("Microsoft-Hyper-V-Hypervisor");

            Assert.Equal(features["Microsoft-Hyper-V-Hypervisor"], status);
        }

        [Fact]
        public void Framework()
        {
#if NETFRAMEWORK
            Assert.Equal(NetFramework.NetFramework, NeonHelper.Framework);
#if NET472_OR_GREATER
            Assert.True(Version.Parse("4.7.2") <= NeonHelper.FrameworkVersion);
            return;
#endif
#elif NET461_OR_GREATER
            Assert.True(Version.Parse("4.6.1") <= NeonHelper.FrameworkVersion);
            Assert.True(NeonHelper.FrameworkVersion < Version.Parse("4.7.2"));
            return;
#endif

#if NET6_0_OR_GREATER
            Assert.Equal(NetFramework.Net, NeonHelper.Framework);
            Assert.Equal(6, NeonHelper.FrameworkVersion.Major);
            return;
#elif NET5_0_OR_GREATER
            Assert.Equal(NetFramework.Net, NeonHelper.Framework);
            Assert.Equal(5, NeonHelper.FrameworkVersion.Major);
            return;
#elif NETCOREAPP
            Assert.Equal(NetFramework.Core, NeonHelper.Framework);
            Assert.Equal(3, NeonHelper.FrameworkVersion.Major);
            return;
#endif
        }
    }
}

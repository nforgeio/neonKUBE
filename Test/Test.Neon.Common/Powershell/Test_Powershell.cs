//-----------------------------------------------------------------------------
// FILE:	    Test_Powershell.cs
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
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Net;
using Neon.Windows;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    [Trait(TestTrait.Category, TestArea.NeonCommon)]
    public class Test_Powershell
    {
        [Fact]
        public void AsText()
        {
            // Verify that any TTY color escape commands are removed.

            using (var powershell = new PowerShell())
            {
                var result = powershell.Execute("Get-ChildItem");

                Assert.NotEmpty(result);
                Assert.DoesNotContain("\x1b[", result);
                Assert.DoesNotContain("\u001b[", result);
            }
        }

        [Fact]
        public void AsJson()
        {
            using (var powershell = new PowerShell())
            {
                var result = powershell.ExecuteJson("Get-ChildItem");

                Assert.NotNull(result);
            }
        }
    }
}

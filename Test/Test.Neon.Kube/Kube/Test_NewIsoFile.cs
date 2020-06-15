//-----------------------------------------------------------------------------
// FILE:	    Test_NewIsoFile.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Xunit;
using Neon.Xunit.Kube;

using Xunit;

namespace TestKube
{
    public class Test_NewIsoFile
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public void CreateIso()
        {
            // Verify that we can create a new ISO file from a folder.

            using (var tempFolder = new TempFolder())
            {
                using (var tempIso = new TempFile(suffix: ".iso"))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        File.WriteAllText(Path.Combine(tempFolder.Path, $"{i}.txt"), $"{i}");
                    }

                    KubeHelper.NewIsoFile(tempFolder.Path, tempIso.Path);

                    using (var file = new FileStream(tempIso.Path, FileMode.Open, FileAccess.Read))
                    {
                        Assert.True(file.Length > 0);
                    }
                }
            }
        }
    }
}

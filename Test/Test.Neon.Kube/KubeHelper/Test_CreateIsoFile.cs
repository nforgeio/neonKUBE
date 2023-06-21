//-----------------------------------------------------------------------------
// FILE:        Test_CreateIsoFile.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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

using DiscUtils.Iso9660;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Xunit;
using Neon.Xunit;

using Xunit;

namespace TestKube
{
    [Trait(TestTrait.Category, TestArea.NeonKube)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_CreateIsoFile
    {
        [Fact]
        public void CreateIso()
        {
            // Verify that we can create a new ISO file from a folder.

            using (var tempFolder = new TempFolder())
            {
                using (var tempIso = new TempFile(suffix: ".iso"))
                {
                    // Create the ISO.

                    for (int i = 0; i < 10; i++)
                    {
                        File.WriteAllText(Path.Combine(tempFolder.Path, $"{i}.txt"), $"{i}");
                    }

                    Directory.CreateDirectory(Path.Combine(tempFolder.Path, "subfolder"));

                    for (int i = 0; i < 10; i++)
                    {
                        File.WriteAllText(Path.Combine(tempFolder.Path, "subfolder", $"{i}.txt"), $"{i}");
                    }

                    KubeHelper.CreateIsoFile(tempFolder.Path, tempIso.Path, "TEST-LABEL");
                    
                    // Validate the ISO.

                    using (var file = new FileStream(tempIso.Path, FileMode.Open, FileAccess.Read))
                    {
                        Assert.True(file.Length > 0);

                        using (var reader = new CDReader(file, joliet: false))
                        {
                            Assert.Equal("TEST-LABEL", reader.VolumeLabel);

                            for (int i = 0; i < 10; i++)
                            {
                                using (var stream = reader.OpenFile($"{i}.txt", FileMode.Open))
                                {
                                    Assert.Equal(i.ToString(), Encoding.UTF8.GetString(stream.ReadToEnd()));
                                }
                            }

                            Assert.True(reader.DirectoryExists("subfolder"));

                            for (int i = 0; i < 10; i++)
                            {
                                var path = $@"subfolder\{i}.txt";

                                using (var stream = reader.OpenFile($@"subfolder\{i}.txt", FileMode.Open))
                                {
                                    Assert.Equal(i.ToString(), Encoding.UTF8.GetString(stream.ReadToEnd()));
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}

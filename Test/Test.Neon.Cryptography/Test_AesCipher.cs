//-----------------------------------------------------------------------------
// FILE:        Test_AesCipher.cs
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
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Xunit;

using Xunit;

namespace TestCryptography
{
    public class Test_AesCipher
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCryptography)]
        public void GenerateKeys()
        {
            var sizes = new int[] { 128, 192, 256 };
            var count = 100;

            // Generate a number of keys of each valid size and ensure that ech key is unique.

            foreach (var size in sizes)
            {
                var keys = new HashSet<string>();

                for (int i = 0; i < count; i++)
                {
                    var key = AesCipher.GenerateKey(size);

                    Assert.NotNull(key);

                    var keyBytes = Convert.FromBase64String(key);

                    Assert.Equal(size, keyBytes.Length * 8);
                    Assert.DoesNotContain(key, keys);

                    keys.Add(key);
                }
            }
        }
    }
}

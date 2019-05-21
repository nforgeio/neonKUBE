//-----------------------------------------------------------------------------
// FILE:        Test_Misc.cs
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Cryptography;
using Neon.Data;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Cadence;

using Xunit;

namespace TestCadence
{
    public sealed class Test_Misc
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void CadenceSettingsJson()
        {
            // Verify that we can serialize [CadenceSettings] to/from JSON.

            var original = new CadenceSettings();

            original.BinaryFolder   = "C:\\temp";
            original.ClientIdentity = "foobar";
            original.ClientTimeout  = TimeSpan.FromSeconds(60);
            original.Servers.Add("http://foo.com");
            original.Servers.Add("http://bar.com");

            var json   = NeonHelper.JsonSerialize(original, Formatting.Indented);
            var parsed = NeonHelper.JsonDeserialize<CadenceSettings>(json);

            Assert.Equal(original.BinaryFolder, parsed.BinaryFolder);
            Assert.Equal(original.ClientIdentity, parsed.ClientIdentity);
            Assert.Equal(original.ClientTimeout, parsed.ClientTimeout);
            Assert.Equal(original.ClientTimeout, parsed.ClientTimeout);

            // Ensure that the default [ClientTimeout] works too.

            original.ClientTimeout = TimeSpan.Zero;

            json   = NeonHelper.JsonSerialize(original, Formatting.Indented);
            parsed = NeonHelper.JsonDeserialize<CadenceSettings>(json);

            Assert.Equal(original.BinaryFolder, parsed.BinaryFolder);
            Assert.Equal(original.ClientIdentity, parsed.ClientIdentity);
            Assert.Equal(original.ClientTimeout, parsed.ClientTimeout);
            Assert.Equal(original.ClientTimeout, parsed.ClientTimeout);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCadence)]
        public void CadenceSettingsYaml()
        {
            // Verify that we can serialize [CadenceSettings] to/from YAML

            var original = new CadenceSettings();

            original.BinaryFolder = "C:\\temp";
            original.ClientIdentity = "foobar";
            original.ClientTimeout = TimeSpan.FromSeconds(60);
            original.Servers.Add("http://foo.com");
            original.Servers.Add("http://bar.com");

            var yaml   = NeonHelper.YamlSerialize(original);
            var parsed = NeonHelper.YamlDeserialize<CadenceSettings>(yaml);

            Assert.Equal(original.BinaryFolder, parsed.BinaryFolder);
            Assert.Equal(original.ClientIdentity, parsed.ClientIdentity);
            Assert.Equal(original.ClientTimeout, parsed.ClientTimeout);
            Assert.Equal(original.ClientTimeout, parsed.ClientTimeout);

            // Ensure that the default [ClientTimeout] works too.

            original.ClientTimeout = TimeSpan.Zero;

            yaml   = NeonHelper.YamlSerialize(original);
            parsed = NeonHelper.YamlDeserialize<CadenceSettings>(yaml);

            Assert.Equal(original.BinaryFolder, parsed.BinaryFolder);
            Assert.Equal(original.ClientIdentity, parsed.ClientIdentity);
            Assert.Equal(original.ClientTimeout, parsed.ClientTimeout);
            Assert.Equal(original.ClientTimeout, parsed.ClientTimeout);
        }
    }
}

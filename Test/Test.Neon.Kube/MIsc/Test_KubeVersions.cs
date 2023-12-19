//-----------------------------------------------------------------------------
// FILE:        Test_KubeVersions.cs
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
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using k8s;
using k8s.Models;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Xunit;
using Neon.Xunit;

using Xunit;

namespace TestKube.Misc
{
    public class Test_KubeVersions
    {
        [Fact]
        public async Task Preprocessor()
        {
            // Verify the [KubeVersions] preprocessor.

            var sbExpected  = new StringBuilder();
            var sbVariables = new StringBuilder();

            foreach (var member in typeof(KubeVersions).GetMembers(BindingFlags.Public | BindingFlags.Static))
            {
                if (member.GetCustomAttribute<KubeVersions.KubeVersionAttribute>() == null)
                {
                    break;
                }

                string value;

                switch (member.MemberType)
                {
                    case MemberTypes.Property:

                        value = typeof(KubeVersions).GetProperty(member.Name).GetValue(null).ToString();
                        break;

                    case MemberTypes.Field:

                        // Constants and field members work the same.

                        var field = (FieldInfo)member;

                        value = field.GetValue(null).ToString();
                        break;

                    default:

                        continue;
                }

                sbExpected.AppendLine($"KubeVersions.{member.Name}={value}");
                sbVariables.Append($"KubeVersions.{member.Name}=$<KubeVersions.{member.Name}>");
            }

            var expected  = sbExpected.ToString();
            var variables = sbVariables.ToString();
            var actual    = (string)null;

            using (var reader = new StringReader(variables))
            {
                using (var preprocessor = KubeVersions.CreatePreprocessor(reader))
                {
                    actual = await preprocessor.ReadToEndAsync();
                }
            }

            Assert.Equal(expected, actual);

            // Check again to ensure that the variable dictionary caching isn't causing trouble.

            using (var reader = new StringReader(variables))
            {
                using (var preprocessor = KubeVersions.CreatePreprocessor(reader))
                {
                    actual = await preprocessor.ReadToEndAsync();
                }
            }

            Assert.Equal(expected, actual);
        }
    }
}

// -----------------------------------------------------------------------------
// FILE:        Test_ConfigExtensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License").
// You may not use this file except in compliance with the License.
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
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

using k8s;
using k8s.KubeConfigModels;
using k8s.Models;

using Neon.Common;
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Config;
using Neon.Kube.Xunit;
using Neon.Xunit;

using Xunit;

namespace TestKube
{
    [Trait(TestTrait.Category, TestArea.NeonKube)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_ConfigExtensions
    {
        //---------------------------------------------------------------------
        // Private types

        public class TestValue
        {
            public int IntValue { get; set; }
            public string StringValue { get; set; }

            public override bool Equals(object obj)
            {
                var other = obj as TestValue;

                if (other == null)
                {
                    return false;
                }

                return this.IntValue == other.IntValue &&
                       this.StringValue == other.StringValue;
            }

            public override int GetHashCode()
            {
                return base.GetHashCode();
            }
        }

        //---------------------------------------------------------------------
        // Implementation

        [Fact]
        public void SetGet()
        {
            var kubeConfig = new KubeConfig();
            var inputValue = new TestValue()
            {
                IntValue    = 100,
                StringValue = "LINE 1\nLINE 2\n"
            };

            kubeConfig.Preferences =
                new KubeConfigPreferences()
                {
                    Extensions = new List<NamedExtension>()
                };

            kubeConfig.Preferences.Extensions.Set("test", inputValue);

            var outputValue = kubeConfig.Preferences.Extensions.Get<TestValue>("test", null);

            Assert.NotNull(outputValue);
            Assert.Equal(inputValue, outputValue);
        }

        [Fact]
        public void Serialize()
        {
            var kubeConfig = new KubeConfig();
            var inputValue = new TestValue()
            {
                IntValue    = 100,
                StringValue = "LINE 1\nLINE 2\n"
            };

            kubeConfig.Preferences =
                new KubeConfigPreferences()
                {
                    Extensions = new List<NamedExtension>()
                };

            kubeConfig.Preferences.Extensions.Set("test", inputValue);

            var serialized = NeonHelper.YamlSerialize(kubeConfig);

            kubeConfig = NeonHelper.YamlDeserialize<KubeConfig>(serialized);

            var outputValue = kubeConfig.Preferences.Extensions.Get<TestValue>("test", null);

            Assert.NotNull(outputValue);
            Assert.Equal(inputValue, outputValue);
        }
    }
}

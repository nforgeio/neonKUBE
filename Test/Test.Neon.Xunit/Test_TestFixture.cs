//-----------------------------------------------------------------------------
// FILE:	    Test_TestFixture.cs
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
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestXunit
{
    /// <summary>
    /// Verify the base test fixture implementation.
    /// </summary>
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_TestFixture : IClassFixture<EnvironmentFixture>
    {
        private EnvironmentFixture  fixture;

        public Test_TestFixture(EnvironmentFixture fixture)
        {
            this.fixture = fixture;

            if (fixture.Start() != TestFixtureStatus.AlreadyRunning)
            {
                fixture.State.Add("value-1", 1);
                fixture.State.Add("value-2", 2);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Verify1()
        {
            // Both state values should be retained across all unit tests.

            Assert.Equal(1, (int)fixture.State["value-1"]);
            Assert.Equal(2, (int)fixture.State["value-2"]);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Verify2()
        {
            // Both state values should be retained across all unit tests.

            Assert.Equal(1, (int)fixture.State["value-1"]);
            Assert.Equal(2, (int)fixture.State["value-2"]);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Verify3()
        {
            // Both state values should be retained across all unit tests.

            Assert.Equal(1, (int)fixture.State["value-1"]);
            Assert.Equal(2, (int)fixture.State["value-2"]);
        }
    }
}

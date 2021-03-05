//-----------------------------------------------------------------------------
// FILE:	    Test_EnvironmentFixture.cs
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
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestXunit
{
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_EnvironmentFixture : IClassFixture<EnvironmentFixture>
    {
        private EnvironmentFixture fixture;

        public Test_EnvironmentFixture(EnvironmentFixture fixture)
        {
            this.fixture = fixture;

            if (fixture.Start() == TestFixtureStatus.AlreadyRunning)
            {
                fixture.Restore();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void EnvironmentVariables()
        {
            // Set some new environment variables.

            Environment.SetEnvironmentVariable("NEON_TEST_VAR0", "VAR0");
            Environment.SetEnvironmentVariable("NEON_TEST_VAR1", "VAR1");

            Assert.Equal("VAR0", Environment.GetEnvironmentVariable("NEON_TEST_VAR0"));
            Assert.Equal("VAR1", Environment.GetEnvironmentVariable("NEON_TEST_VAR1"));

            // Verify that [Restore()] deletes them.

            fixture.Restore();

            Assert.Null(Environment.GetEnvironmentVariable("NEON_TEST_VAR0"));
            Assert.Null(Environment.GetEnvironmentVariable("NEON_TEST_VAR1"));

            // Delete a prexisitng environment variable and verify that [Restore()]
            // actually restores it.

            var orgPath = Environment.GetEnvironmentVariable("PATH");

            Assert.NotEmpty(orgPath);
            Environment.SetEnvironmentVariable("PATH", null);
            Assert.Null(Environment.GetEnvironmentVariable("PATH"));

            fixture.Restore();

            Assert.Equal(orgPath, Environment.GetEnvironmentVariable("PATH"));
        }
    }
}

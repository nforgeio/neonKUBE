//-----------------------------------------------------------------------------
// FILE:	    Test_ContainerFixture.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;
using Neon.Xunit;

using Xunit;

namespace TestXunit
{
    [Trait(TestTrait.Category, TestArea.NeonXunit)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_ContainerFixture : IClassFixture<ContainerFixture>
    {
        private ContainerFixture fixture;

        public Test_ContainerFixture(ContainerFixture fixture)
        {
            TestHelper.ResetDocker(this.GetType());

            this.fixture = fixture;

            fixture.Start("neon-unit-test-container", $"{NeonHelper.NeonLibraryBranchRegistry}/test:latest");
        }

        [Fact]
        public void Basic()
        {
            // Verify that we can start a simple container with defaults.

            var result = NeonHelper.ExecuteCapture(NeonHelper.DockerCli, "ps");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("neon-unit-test-container", result.AllText);
        }
    }
}

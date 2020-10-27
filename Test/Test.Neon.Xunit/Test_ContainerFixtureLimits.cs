//-----------------------------------------------------------------------------
// FILE:	    Test_ContainerFixtureLimits.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
    public class Test_ContainerFixtureLimits : IClassFixture<ContainerFixture>
    {
        private ContainerFixture fixture;

        public Test_ContainerFixtureLimits(ContainerFixture fixture)
        {
            this.fixture = fixture;

            var limits = new ContainerLimits()
            {
                Memory            = "50 MiB",
                MemoryReservation = "40 MiB",
                MemorySwap        = "100 MiB",
                MemorySwappiness  = 50,
                KernelMemory      = "4 MiB",
                OomKillDisable    = true
            };

            fixture.Start("neon-unit-test-containerr", $"{NeonHelper.NeonBranchRegistry}/test:latest", limits: limits);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Basic()
        {
            // Verify that we can start a simple container with resource limits.

            var result = NeonHelper.ExecuteCapture(NeonHelper.DockerCli, "ps");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("neon-unit-test-container", result.AllText);
        }
    }
}

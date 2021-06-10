//-----------------------------------------------------------------------------
// FILE:	    Test_DockerComposeFixture.cs
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
using System.Reflection.Metadata;
using System.Text;
using System.Text.RegularExpressions;
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
    public class Test_DockerComposeFixture : IClassFixture<DockerComposeFixture>
    {
        private const string alpineDefinition =
@"version: '3'
services:
  alpine:
    image: ""alpine:latest""
    entrypoint: /bin/sh
    command: -c ""sleep 1000000""
";

        private DockerComposeFixture fixture;

        public Test_DockerComposeFixture(DockerComposeFixture fixture)
        {
            TestHelper.ResetDocker(this.GetType());

            this.fixture = fixture;

            fixture.Start("neon-unit-test-stack", alpineDefinition);
        }

        [Fact]
        public void Basic()
        {
            // All we need to do is verify that the application containers are running
            // and that the default application network exists.

            var result = NeonHelper.ExecuteCapture(NeonHelper.DockerCli, new string[] { "ps" });

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("neon-unit-test-stack_", result.AllText);

            result = NeonHelper.ExecuteCapture(NeonHelper.DockerCli, new string[] { "network", "ls" });

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("neon-unit-test-stack_default", result.AllText);
        }

        [Fact]
        public void Restart()
        {
            // We're going to verify that the application was actually restarted by verifying
            // that the new container ID was assigned a new ID.

            var idRegex = new Regex(@"^neon-unit-test-stack_.+$", RegexOptions.Multiline);
            var result  = NeonHelper.ExecuteCapture(NeonHelper.DockerCli, new object[] { "ps", "--format", "{{.Names}}.{{.ID}}" });

            Assert.Equal(0, result.ExitCode);

            var orgId = idRegex.Match(result.AllText).Value;

            fixture.Restart();

            result = NeonHelper.ExecuteCapture(NeonHelper.DockerCli, new object[] { "ps", "--format", "{{.Names}}.{{.ID}}" });
            
            Assert.Equal(0, result.ExitCode);

            var newId = idRegex.Match(result.AllText).Value; ;

            Assert.NotEqual(orgId, newId);
        }
    }
}

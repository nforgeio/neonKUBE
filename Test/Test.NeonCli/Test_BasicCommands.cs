//-----------------------------------------------------------------------------
// FILE:        Test_BasicCommands.cs
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

using Neon;
using Neon.Common;
using Neon.Kube;
using Neon.Kube.BuildInfo;
using Neon.Kube.Xunit;
using Neon.IO;
using Neon.Xunit;

using Xunit;

using NeonCli;

namespace Test.NeonCli
{
    [Trait(TestTrait.Category, TestArea.NeonCli)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_BasicCommands
    {
        [Fact]
        public async Task Base()
        {
            using (var runner = new ProgramRunner())
            {
                // Verify that base command returns some help.

                var result = await runner.ExecuteAsync(Program.Main, "login");

                Assert.Equal(0, result.ExitCode);
                Assert.Contains("USAGE:", result.OutputText);

                // Verify that we see an error for an unrecognized command.,

                result = await runner.ExecuteAsync(Program.Main, "bad-command", "invalid-command");

                Assert.NotEqual(0, result.ExitCode);
            }
        }

        [Fact]
        public async Task Version()
        {
            using (var runner = new ProgramRunner())
            {
                var result = await runner.ExecuteAsync(Program.Main, "version");
                Assert.Equal(0, result.ExitCode);
                Assert.Equal(KubeVersions.Kubernetes, result.OutputText.Trim());

                result = await runner.ExecuteAsync(Program.Main, "version", "-n");
                Assert.Equal(0, result.ExitCode);
                Assert.Equal(KubeVersions.Kubernetes, result.OutputText.Trim());
                Assert.DoesNotContain('\n', result.OutputText);

                result = await runner.ExecuteAsync(Program.Main, "version", "-n", "--git");
                Assert.Equal(0, result.ExitCode);
                Assert.Equal($"{KubeVersions.Kubernetes}/{ThisAssembly.Git.Branch}-{ThisAssembly.Git.Commit}", result.OutputText.Trim());
                Assert.DoesNotContain('\n', result.OutputText);

                result = await runner.ExecuteAsync(Program.Main, "version", $"--minimum={Program.Version}");
                Assert.Equal(0, result.ExitCode);

                result = await runner.ExecuteAsync(Program.Main, "version", $"--minimum=0");
                Assert.Equal(0, result.ExitCode);

                result = await runner.ExecuteAsync(Program.Main, "version", $"--minimum=64000.0.0");
                Assert.NotEqual(0, result.ExitCode);

                var curVersion   = SemanticVersion.Parse(Program.Version);
                var newerVersion = SemanticVersion.Create(curVersion.Major, curVersion.Minor, curVersion.Patch + 1, curVersion.Build, curVersion.Prerelease);

                Assert.True(newerVersion > curVersion);

                result = await runner.ExecuteAsync(Program.Main, "version", $"--minimum={newerVersion}");
                Assert.NotEqual(0, result.ExitCode);
            }
        }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    Test_DockerContainerFixture.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_ProgramRunner
    {
        private bool mainExecuted = false;

        private int Main(string[] args)
        {
            var commandLine = new CommandLine(args);
            var command     = commandLine.Arguments.FirstOrDefault();

            mainExecuted = true;

            switch (command)
            {
                case "pass":

                    return 0;

                case "fail":

                    return 1;

                case "fork":

                    if (ProgramRunner.Current != null)
                    {
                        ProgramRunner.Current.ProgramReady();
                        ProgramRunner.Current.WaitForExit();
                    }
                    return 0;
            }

            return 1;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Execute()
        {
            using (var runner = new ProgramRunner())
            {
                mainExecuted = false;
                Assert.Equal(0, runner.Execute(Main, "pass"));
                Assert.True(mainExecuted);

                mainExecuted = false;
                Assert.Equal(1, runner.Execute(Main, "fail"));
                Assert.True(mainExecuted);
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Fork()
        {
            using (var runner = new ProgramRunner())
            {
                mainExecuted = false;
                runner.Fork(Main, "fork");
                runner.TerminateFork();
                Assert.True(mainExecuted);
            }
        }
    }
}

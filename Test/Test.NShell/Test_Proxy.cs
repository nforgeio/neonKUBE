//-----------------------------------------------------------------------------
// FILE:	    Test_Proxy.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Kube;

using Xunit;
using System.Net;

namespace Test.NShell
{
    /// <summary>
    /// Tests the <b>nshell proxy</b> command.
    /// </summary>
    public class Test_Proxy
    {
        private string      nshellPath;
        private Process     nshellProcess;

        public Test_Proxy()
        {
            nshellPath = Path.Combine(Environment.GetEnvironmentVariable("NF_BUILD_NSHELL"), NeonHelper.IsWindows ? "nshell.exe" : "nshell");
        }

        /// <summary>
        /// Executes <b>nshell</b> synchronously, passing arguments and returning the result.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns>The <see cref="ExecuteResult"/>.</returns>
        private ExecuteResult NShell(params object[] args)
        {
            return NeonHelper.ExecuteCapture(nshellPath, args);
        }

        /// <summary>
        /// Executes <b>nshell</b> asynchronously, without waiting for the command to complete.
        /// This is useful for commands that don't terminate by themselves.  Call <see cref="NShellTerminateAsync()"/>
        /// to kill the running nshell process.
        /// </summary>
        /// <param name="args">The arguments.</param>
        /// <returns>The tracking task.</returns>
        private async Task NShellAsync(params object[] args)
        {
            if (nshellProcess != null)
            {
                throw new InvalidOperationException("Only one [nshell] process can run at a time.");
            }

            using (nshellProcess = new Process())
            {
                await NeonHelper.ExecuteCaptureAsync(nshellPath, args, process: nshellProcess);
            }

            nshellProcess = null;
        }

        /// <summary>
        /// Terminates the <b>nshell</b> process if one is running.
        /// </summary>
        private async Task NShellTerminateAsync()
        {
            nshellProcess?.Kill();
            await NeonHelper.WaitForAsync(async () => await Task.FromResult(nshellProcess == null), TimeSpan.FromSeconds(60));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonShell)]
        public async Task ProxyBasics()
        {
            // Verifies that [nshell proxy ...] can actually proxy traffic.

            // Select endpoints that are unlikely to be already in use and
            // run the proxy command asynchronously.

            var localEndpoint  = new IPEndPoint(IPAddress.Parse("127.0.0.57"), 61422);
            var remoteEndpoint = new IPEndPoint(IPAddress.Parse("127.0.0.57"), 61423);
            var shellTask      = NShellAsync($"proxy unit-test {localEndpoint} {remoteEndpoint}");

            await NShellTerminateAsync();
            await shellTask;
        }
    }
}

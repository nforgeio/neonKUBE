//-----------------------------------------------------------------------------
// FILE:	    Test_NeonHelper.Process.cs
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
using System.IO;
using System.Text;

using Neon.Common;
using Neon.Xunit;

using Newtonsoft.Json;
using Xunit;

namespace TestCommon
{
    public partial class Test_NeonHelper
    {
        /// <summary>
        /// Returns the path to the [Test.ExecTarget] binary.
        /// </summary>
        private string ExecTargetPath
        {
            get
            {
#if DEBUG
                var confguration = "Debug";
#else
                var confguration = "Release";
#endif
                return Path.Combine(Environment.GetEnvironmentVariable("NK_ROOT"), "Test", "Test.ExecTarget", "bin", confguration, "net6.0", "Test.ExecTarget.exe");
            }
        }

        //---------------------------------------------------------------------
        // UTF-8

        [Fact]
        public void Execute_Utf8_StdOutput()
        {
            // Ensure that we can capture standard output encoded as UTF-8.

            var response = NeonHelper.ExecuteCapture(ExecTargetPath,
                new object[]
                {
                    $"--encoding=utf-8",
                    "--exitcode=0",
                    "--lines=1",
                    "--text=Hello World!",
                    "--write-output"
                },
                outputEncoding: Encoding.UTF8);

            Assert.Equal(0, response.ExitCode);

            using (var reader = new StringReader(response.OutputText))
            {
                Assert.Equal("Hello World!", reader.ReadLine());
            }
        }

        [Fact]
        public void Execute_Utf8_StdError()
        {
            // Ensure that we can capture standard error encoded as UTF-8.

            var response = NeonHelper.ExecuteCapture(ExecTargetPath,
                new object[]
                {
                    "--encoding=utf-8",
                    "--exitcode=0",
                    "--lines=1",
                    "--text=Hello World!",
                    "--write-error"
                },
                outputEncoding: Encoding.UTF8);

            Assert.Equal(0, response.ExitCode);

            using (var reader = new StringReader(response.ErrorText))
            {
                Assert.Equal("Hello World!", reader.ReadLine());
            }
        }

        [Fact]
        public void Execute_Utf_Both()
        {
            // Ensure that we can capture standard error encoded as UTF-8.

            var response = NeonHelper.ExecuteCapture(ExecTargetPath,
                new object[]
                {
                    "--encoding=utf-8",
                    "--exitcode=0",
                    "--lines=1",
                    "--text=Hello World!",
                    "--write-output",
                    "--write-error"
                },
                outputEncoding: Encoding.UTF8);

            Assert.Equal(0, response.ExitCode);

            using (var reader = new StringReader(response.OutputText))
            {
                Assert.Equal("Hello World!", reader.ReadLine());
            }

            using (var reader = new StringReader(response.ErrorText))
            {
                Assert.Equal("Hello World!", reader.ReadLine());
            }
        }

        //---------------------------------------------------------------------
        // Unicode

        [Fact]
        public void Execute_Unicode_StdOutput()
        {
            // Ensure that we can capture standard output encoded as Unicode.

            var response = NeonHelper.ExecuteCapture(ExecTargetPath,
                new object[]
                {
                    "--encoding=unicode",
                    "--exitcode=0",
                    "--lines=1",
                    "--text=Hello World!",
                    "--write-output"
                },
                outputEncoding: Encoding.Unicode);

            Assert.Equal(0, response.ExitCode);

            using (var reader = new StringReader(response.OutputText))
            {
                Assert.Equal("Hello World!", reader.ReadLine());
            }
        }

        [Fact]
        public void Execute_Unicode_StdError()
        {
            // Ensure that we can capture standard error encoded as Unicode.

            var response = NeonHelper.ExecuteCapture(ExecTargetPath,
                new object[]
                {
                    "--encoding=unicode",
                    "--exitcode=0",
                    "--lines=1",
                    "--text=Hello World!",
                    "--write-error"
                },
                outputEncoding: Encoding.Unicode);

            Assert.Equal(0, response.ExitCode);

            using (var reader = new StringReader(response.ErrorText))
            {
                Assert.Equal("Hello World!", reader.ReadLine());
            }
        }

        [Fact]
        public void Execute_Unicode_Both()
        {
            // Ensure that we can capture standard error encoded as Unicode.

            var response = NeonHelper.ExecuteCapture(ExecTargetPath,
                new object[]
                {
                    "--encoding=unicode",
                    "--exitcode=0",
                    "--lines=1",
                    "--text=Hello World!",
                    "--write-output",
                    "--write-error"
                },
                outputEncoding: Encoding.Unicode);

            Assert.Equal(0, response.ExitCode);

            using (var reader = new StringReader(response.OutputText))
            {
                Assert.Equal("Hello World!", reader.ReadLine());
            }

            using (var reader = new StringReader(response.ErrorText))
            {
                Assert.Equal("Hello World!", reader.ReadLine());
            }
        }
    }
}

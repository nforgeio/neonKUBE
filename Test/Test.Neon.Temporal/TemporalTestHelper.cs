//-----------------------------------------------------------------------------
// FILE:        TemporalTestHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Data;
using Neon.Diagnostics;
using Neon.IO;
using Neon.Temporal;
using Neon.Temporal.Internal;
using Neon.Xunit;
using Neon.Xunit.Temporal;

using Newtonsoft.Json;
using Xunit;

using Test.Neon.Models;
using Newtonsoft.Json.Linq;

namespace TestTemporal
{
    /// <summary>
    /// Internal unit test helpers.
    /// </summary>
    internal static class TemporalTestHelper
    {
        /// <summary>
        /// Specifies the Docker stack definition used to execute Temporal unit tests.
        /// </summary>
        public const string TemporalStackDefinition = TemporalFixture.DefaultStackDefinition;

        /// <summary>
        /// <para>
        /// Controls whether <see cref="TemporalFixture"/> should be configured to leave
        /// the Temporal test container running after the unit tests complete.  This can
        /// be handy during debugging by keeping the Temporal UX around for post test
        /// investigations.
        /// </para>
        /// <note>
        /// This should be disabled for normal CI/CD environments.
        /// </note>
        /// </summary>
        public const bool KeepTemporalServerOpen = true;

        /// <summary>
        /// Specifies the log level to use for Temporal related unit tests.
        /// </summary>
        public static readonly LogLevel LogLevel = LogLevel.Info;

        /// <summary>
        /// Specifies whether Temporal unit tests will connect to Temporal in DEBUG mode
        /// which launches <b>temporal-proxy</b> in a CMD shell on Windows.
        /// </summary>
        public const bool Debug = false;

        /// <summary>
        /// <para>
        /// Optionally runs Temporal in prelaunched mode for unit testing.
        /// </para>
        /// <note>
        /// This must always be reset to <c>false</c> after any manual debugging so
        /// that unit tests will work properly.
        /// </note>
        /// </summary>
        public const bool DebugPrelaunched = false;

        /// <summary>
        /// <para>
        /// Optionally runs Temporal in without heartbeats for unit testing.
        /// </para>
        /// <note>
        /// This must always be reset to <c>false</c> after any manual debugging so
        /// that unit tests will work properly.
        /// </note>
        /// </summary>
        public const bool DebugDisableHeartbeats = false;

        /// <summary>
        /// The Temporal task list to be used for test workers.
        /// </summary>
        public const string TaskList = "tests";

        /// <summary>
        /// The Temporal task list to be use for interop testing against the
        /// <b>twf-args.exe</b> worker written in GOLANG.
        /// </summary>
        public const string TaskList_TwfArgs = "twf-args";

        /// <summary>
        /// Identifies the test clients.
        /// </summary>
        public const string ClientIdentity = "unit-test";
    }
}

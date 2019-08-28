//-----------------------------------------------------------------------------
// FILE:        CadenceTestHelper.cs
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Neon.Cadence;
using Neon.Cadence.Internal;
using Neon.Common;
using Neon.Data;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Cadence;

using Newtonsoft.Json;
using Xunit;

using Test.Neon.Models;
using Newtonsoft.Json.Linq;

namespace TestCadence
{
    /// <summary>
    /// Internal unit test helpers.
    /// </summary>
    internal static class CadenceTestHelper
    {
        /// <summary>
        /// <para>
        /// Controls whether <see cref="CadenceFixture"/> should be configured to leave
        /// the Cadence test container running after the unit tests complete.  This can
        /// be handy during debugging by keeping the Cadence UX around for post test
        /// invesitigations.
        /// </para>
        /// <note>
        /// This should be disabled for normal CI/CD environments.
        /// </note>
        /// </summary>
        public const bool KeepCadenceServerOpen = true;     // $debug(jeff.lill): Set this back to FALSE.

        /// <summary>
        /// Used by <see cref="SlowFactAttribute"/> to control whether slow tests
        /// are executed or not.
        /// </summary>
        public static bool RunSlowTests { get; set; } = false;
    }
}

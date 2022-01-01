//-----------------------------------------------------------------------------
// FILE:	    TestFixtureStatus.cs
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

using Xunit;

using Neon.Common;

namespace Neon.Xunit
{
    /// <summary>
    /// Returned by <see cref="ITestFixture.Start(Action)"/> to indicate whether
    /// the test fixture was just started or was already running.
    /// </summary>
    public enum TestFixtureStatus
    {
        /// <summary>
        /// The fixture was just started.
        /// </summary>
        Started,

        /// <summary>
        /// The fixture was already running.
        /// </summary>
        AlreadyRunning
    }
}

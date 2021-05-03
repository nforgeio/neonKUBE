//-----------------------------------------------------------------------------
// FILE:	    TestTrait.cs
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Xunit
{
    /// <summary>
    /// Identifies the common neonFORGE related test traits.
    /// </summary>
    public static class TestTrait
    {
        /// <summary>
        /// Identifies the test project.  See <see cref="TestArea"/> for the standard
        /// project names.
        /// </summary>
        public const string Area = "area";

        /// <summary>
        /// Identifies slow tests by setting this trait's value to <b>"1"</b>.
        /// </summary>
        public const string Slow = "slow";

        /// <summary>
        /// Identifies unreliable tests that fail due to transient environmental
        /// issues generally out of control of the test case developer by setting
        /// the value to <b>"1"</b>.
        /// </summary>
        public const string Unreliable = "unreliable";

        /// <summary>
        /// Identifies test cases that appear to have bugs as opposed to the thing
        /// being tested having bugs.  Set the value to <b>"1"</b>.
        /// </summary>
        public const string Buggy = "buggy";

        /// <summary>
        /// Identifies test cases that are still under development by setting the\
        /// value to <b>"1"</b>.
        /// </summary>
        public const string Incomplete = "incomplete";

        /// <summary>
        /// Identifies test cases that are failing and are actively under investigation
        /// by setting the value to <b>"1"</b>.
        /// </summary>
        public const string Investigate = "investigate";
    }
}

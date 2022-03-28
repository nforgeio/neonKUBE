//-----------------------------------------------------------------------------
// FILE:	    TestTrait.cs
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
        /// Identifies the <b>Category</b> test trait.
        /// </summary>
        public const string Category = "Category";

        /// <summary>
        /// Set this as the category value for slow tests.
        /// </summary>
        public const string Slow = "slow";

        /// <summary>
        /// Set this as the category value for tests that require a neonKUBE cluster.
        /// </summary>
        public const string NeonKube = "neon-kube";

        /// <summary>
        /// Set as the category value to identify test cases that appear to have
        /// bugs as opposed to the thing being tested having bugs.  This also 
        /// covers transient environmental issues generally out of control of the
        /// test case developer.
        /// </summary>
        public const string Buggy = "buggy";

        /// <summary>
        /// Set as the category value to identify test cases that are still under
        /// development.
        /// </summary>
        public const string Incomplete = "incomplete";

        /// <summary>
        /// Set as the category value to identify test cases that are failing and 
        /// are actively under investigation.
        /// </summary>
        public const string Investigate = "investigate";
    }
}

//-----------------------------------------------------------------------------
// FILE:        ClusterTheoryAttribute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:   Copyright © 2005-2024 by NEONFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Reflection;
using System.Text;

using Neon.Xunit;

using Xunit;
using Xunit.Sdk;

namespace Neon.Kube.Xunit
{
    /// <summary>
    /// <para>
    /// Used for marking test throry cases that access or provision NEONKUBE clusters by
    /// extending <see cref="TheoryAttribute"/> and skipping these tests when they
    /// cannot or should not be executed on the current machine.
    /// </para>
    /// <para>
    /// This works by looking for the presence of the <b>NEON_CLUSTER_TESTING</b>
    /// environment variable.  This attribute will run the test case when this
    /// variable exists otherwise the test case will be skipped.
    /// </para>
    /// </summary>
    public class ClusterTheoryAttribute : TheoryAttribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public ClusterTheoryAttribute()
        {
            if (!TestHelper.IsClusterTestingEnabled)
            {
                Skip = $"Disabled because the [{TestHelper.ClusterTestingVariable}] environment variable does not exist.";
            }
        }
    }
}

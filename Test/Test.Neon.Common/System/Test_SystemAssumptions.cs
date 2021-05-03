//-----------------------------------------------------------------------------
// FILE:	    Test_SystemAssumptions.cs
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
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Retry;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    /// <summary>
    /// This verifies assumptions about the standard .NET class libraries.
    /// </summary>
    public class Test_SystemAssumptions
    {
        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void IEnumerabe_ToList()
        {
            // Verify that [IEnumerable.ToList()] on a list actually returns a copy of the list
            // not the source list itself.

            var source = new List<int>() { 0, 1, 2, 3, 4, 5 };
            var copy   = source.ToList();

            Assert.Equal(source, copy);
            Assert.NotSame(source, copy);
        }
    }
}

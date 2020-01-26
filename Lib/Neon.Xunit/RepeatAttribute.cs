//-----------------------------------------------------------------------------
// FILE:        RepeatAttribute.cs
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
using System.Diagnostics.Contracts;
using System.Linq;
using System.Reflection;
using System.Text;

using Xunit;
using Xunit.Sdk;

namespace Neon.Xunit
{
    /// <summary>
    /// Used to annotate an Xunit <c>[Theory]</c> test method to have the test executed
    /// the specified number of times.
    /// </summary>
    public class RepeatAttribute : DataAttribute
    {
        private int count;

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="count">Specifies the number of times the theory should be executed.</param>
        public RepeatAttribute(int count)
        {
            Covenant.Requires<ArgumentException>(count > 0, nameof(count));

            this.count = count;
        }

        /// <inheritdoc/>
        public override IEnumerable<object[]> GetData(MethodInfo testMethod)
        {
            var iterations = new object[count][];

            for (int i = 0; i < count; i++)
            {
                iterations[i] = new object[] { i };
            }

            return (IEnumerable<object[]>)iterations;
        }
    }
}

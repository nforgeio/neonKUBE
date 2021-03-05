//-----------------------------------------------------------------------------
// FILE:	    Test_IEnumerableExtensions.cs
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
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Collections;
using Neon.Net;
using Neon.Retry;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_IEnumerableExtensions
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void SelectRandom_Single()
        {
            var items = new int[] { 0, 1, 2, 3 };

            // Perform a selection up to 1 million times to ensure that we're
            // actually getting different values (there's a million-to-one
            // chance that this test could report an invalid failure).

            var selected = items.SelectRandom();

            Assert.Single(selected);

            var item = selected.Single();

            for (int i = 0; i < 1000000; i++)
            {
                if (items.SelectRandom().Single() != item)
                {
                    return;
                }
            }

            Assert.True(false, "Random selection may not be working (there's a one-in-a-million chance that this is an invalid failure).");
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void SelectRandom_Multiple()
        {
            var items    = new int[] { 0, 1, 2, 3 };
            var selected = items.SelectRandom(2);

            Assert.Equal(2, selected.Count());                              // Ensure that we got two items
            Assert.NotEqual(selected.First(), selected.Skip(1).First());    // Ensure that the items are different
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void SelectRandom_Exceptions()
        {
            Assert.Throws<ArgumentNullException>(() => ((IEnumerable<int>)null).SelectRandom());

            var items = new int[] { 0, 1, 2, 3 };

            Assert.Throws<ArgumentException>(() => items.SelectRandom(-1));
            Assert.Throws<ArgumentException>(() => items.SelectRandom(0));
            Assert.Throws<ArgumentException>(() => items.SelectRandom(5));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void IsEmpty()
        {
            // Verify that both reference and value types work.

            Assert.True((new List<string>()).IsEmpty());
            Assert.True((new List<int>()).IsEmpty());

            Assert.False((new List<string>() { "one", "two" }).IsEmpty());
            Assert.False((new List<int>() { 1, 2 }).IsEmpty());
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Predicate()
        {
            // Verify that predicates work.

            var items = new int[] { 0, 1, 2, 3, 4 };

            Assert.False(items.IsEmpty(item => true));
            Assert.True(items.IsEmpty(item => false));

            Assert.False(items.IsEmpty(item => item < 2));
            Assert.True(items.IsEmpty(item => item > 4));
        }
    }
}

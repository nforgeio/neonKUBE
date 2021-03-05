//-----------------------------------------------------------------------------
// FILE:	    Test_TestHelper.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestXunit
{
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_TestHelper
    {
        public class Item
        {
            public Item(string value)
            {
                this.Value = value;
            }

            public string Value { get; set; }
        }

        public class ItemComparer : IEqualityComparer<Item>
        {
            public bool Equals(Item x, Item y)
            {
                if (x == null && y == null)
                {
                    return true;
                }
                else if (x == null && y != null || y != null && x == null)
                {
                    return false;
                }

                var xValue = x.Value;
                var yValue = y.Value;

                if (xValue == null && yValue == null)
                {
                    return true;
                }
                else if (xValue == null && yValue != null || yValue != null && xValue == null)
                {
                    return false;
                }

                return xValue == yValue;
            }

            public int GetHashCode(Item obj)
            {
                if (obj == null)
                {
                    return 0;
                }

                return obj.Value.GetHashCode();
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Enumerable_Equivalent()
        {
            TestHelper.AssertEquivalent(new List<string>(), new List<string>());
            TestHelper.AssertEquivalent(new List<string>() { "0" }, new List<string>() { "0" });
            TestHelper.AssertEquivalent(new List<string>() { "0", "1" }, new List<string>() { "0", "1" });
            TestHelper.AssertEquivalent(new List<string>() { "0", "1" }, new List<string>() { "1", "0" });

            Assert.ThrowsAny<Exception>(() => TestHelper.AssertEquivalent(new List<string>(), new List<string>() { "0" }));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertEquivalent(new List<string>() { "0" }, new List<string>()));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertEquivalent(new List<string>() { "0" }, new List<string>() { "1" }));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertEquivalent(new List<string>() { "0" }, new List<string>() { "0", "1" }));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Enumerable_NotEquivalent()
        {
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertNotEquivalent(new List<string>(), new List<string>()));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertNotEquivalent(new List<string>() { "0" }, new List<string>() { "0" }));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertNotEquivalent(new List<string>() { "0", "1" }, new List<string>() { "0", "1" }));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertNotEquivalent(new List<string>() { "0", "1" }, new List<string>() { "1", "0" }));

            TestHelper.AssertNotEquivalent(new List<string>(), new List<string>() { "0" });
            TestHelper.AssertNotEquivalent(new List<string>() { "0" }, new List<string>());
            TestHelper.AssertNotEquivalent(new List<string>() { "0" }, new List<string>() { "1" });
            TestHelper.AssertNotEquivalent(new List<string>() { "0" }, new List<string>() { "0", "1" });
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Enumerable_EquivalentComparer()
        {
            var comparer = new ItemComparer();

            TestHelper.AssertEquivalent(new List<Item>(), new List<Item>(), comparer);
            TestHelper.AssertEquivalent(new List<Item>() { new Item("0") }, new List<Item>() { new Item("0") }, comparer);
            TestHelper.AssertEquivalent(new List<Item>() { new Item("0"), new Item("1") }, new List<Item>() { new Item("0"), new Item("1") }, comparer);
            TestHelper.AssertEquivalent(new List<Item>() { new Item("0"), new Item("1") }, new List<Item>() { new Item("1"), new Item("0") }, comparer);

            Assert.ThrowsAny<Exception>(() => TestHelper.AssertEquivalent(new List<Item>(), new List<Item>() { new Item("0") }, comparer));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertEquivalent(new List<Item>() { new Item("0") }, new List<Item>(), comparer));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertEquivalent(new List<Item>() { new Item("0") }, new List<Item>() { new Item("1") }, comparer));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertEquivalent(new List<Item>() { new Item("0") }, new List<Item>() { new Item("0"), new Item("1") }, comparer));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Enumerable_NotEquivalentComparer()
        {
            var comparer = new ItemComparer();

            Assert.ThrowsAny<Exception>(() => TestHelper.AssertNotEquivalent(new List<Item>(), new List<Item>(), comparer));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertNotEquivalent(new List<Item>() { new Item("0") }, new List<Item>() { new Item("0") }, comparer));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertNotEquivalent(new List<Item>() { new Item("0"), new Item("1") }, new List<Item>() { new Item("0"), new Item("1") }, comparer));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertNotEquivalent(new List<Item>() { new Item("0"), new Item("1") }, new List<Item>() { new Item("1"), new Item("0") }, comparer));

            TestHelper.AssertNotEquivalent(new List<Item>(), new List<Item>() { new Item("0") }, comparer);
            TestHelper.AssertNotEquivalent(new List<Item>() { new Item("0") }, new List<Item>(), comparer);
            TestHelper.AssertNotEquivalent(new List<Item>() { new Item("0") }, new List<Item>() { new Item("1") }, comparer);
            TestHelper.AssertNotEquivalent(new List<Item>() { new Item("0") }, new List<Item>() { new Item("0"), new Item("1") }, comparer);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Collection_Equivalent()
        {
            TestHelper.AssertEquivalent(new Dictionary<string, string>(), new Dictionary<string, string>());
            TestHelper.AssertEquivalent(new Dictionary<string, string>() { { "0", "0" } }, new Dictionary<string, string>() { { "0", "0" } });
            TestHelper.AssertEquivalent(new Dictionary<string, string>() { { "0", "0" }, { "1", "1" } }, new Dictionary<string, string>() { { "0", "0" }, { "1", "1" } });

            Assert.ThrowsAny<Exception>(() => TestHelper.AssertEquivalent(new Dictionary<string, string>(), new Dictionary<string, string>() { { "0", "0" } }));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertEquivalent(new Dictionary<string, string>() { { "0", "0" } }, new Dictionary<string, string>()));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertEquivalent(new Dictionary<string, string>() { { "0", "0" } }, new Dictionary<string, string>() { { "1", "1" } }));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertEquivalent(new Dictionary<string, string>() { { "0", "0" } }, new Dictionary<string, string>() { { "0", "0" }, { "1", "1" } }));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Collection_NotEquivalent()
        {
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertNotEquivalent(new Dictionary<string, string>(), new Dictionary<string, string>()));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertNotEquivalent(new Dictionary<string, string>() { { "0", "0" } }, new Dictionary<string, string>() { { "0", "0" } }));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertNotEquivalent(new Dictionary<string, string>() { { "0", "0" }, { "1", "1" } }, new Dictionary<string, string>() { { "0", "0" }, { "1", "1" } }));

            TestHelper.AssertNotEquivalent(new Dictionary<string, string>(), new Dictionary<string, string>() { { "0", "0" } });
            TestHelper.AssertNotEquivalent(new Dictionary<string, string>() { { "0", "0" } }, new Dictionary<string, string>());
            TestHelper.AssertNotEquivalent(new Dictionary<string, string>() { { "0", "0" } }, new Dictionary<string, string>() { { "1", "1" } });
            TestHelper.AssertNotEquivalent(new Dictionary<string, string>() { { "0", "0" } }, new Dictionary<string, string>() { { "0", "0" }, { "1", "1" } });
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Collection_EquivalentComparer()
        {
            var comparer = new ItemComparer();

            TestHelper.AssertEquivalent(new Dictionary<string, Item>(), new Dictionary<string, Item>(), comparer);
            TestHelper.AssertEquivalent(new Dictionary<string, Item>() { { "0", new Item("0") } }, new Dictionary<string, Item>() { { "0", new Item("0") } }, comparer);
            TestHelper.AssertEquivalent(new Dictionary<string, Item>() { { "0", new Item("0") }, { "1", new Item("1") } }, new Dictionary<string, Item>() { { "0", new Item("0") }, { "1", new Item("1") } }, comparer);

            Assert.ThrowsAny<Exception>(() => TestHelper.AssertEquivalent(new Dictionary<string, Item>(), new Dictionary<string, Item>() { { "0", new Item("0") } }, comparer));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertEquivalent(new Dictionary<string, Item>() { { "0", new Item("0") } }, new Dictionary<string, Item>(), comparer));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertEquivalent(new Dictionary<string, Item>() { { "0", new Item("0") } }, new Dictionary<string, Item>() { { "1", new Item("1") } }, comparer));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertEquivalent(new Dictionary<string, Item>() { { "0", new Item("0") } }, new Dictionary<string, Item>() { { "0", new Item("0") }, { "1", new Item("1") } }, comparer));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Collection_NotEquivalentComparer()
        {
            var comparer = new ItemComparer();

            Assert.ThrowsAny<Exception>(() => TestHelper.AssertNotEquivalent(new Dictionary<string, Item>(), new Dictionary<string, Item>(), comparer));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertNotEquivalent(new Dictionary<string, Item>() { { "0", new Item("0") } }, new Dictionary<string, Item>() { { "0", new Item("0") } }, comparer));
            Assert.ThrowsAny<Exception>(() => TestHelper.AssertNotEquivalent(new Dictionary<string, Item>() { { "0", new Item("0") }, { "1", new Item("1") } }, new Dictionary<string, Item>() { { "0", new Item("0") }, { "1", new Item("1") } }, comparer));

            TestHelper.AssertNotEquivalent(new Dictionary<string, Item>(), new Dictionary<string, Item>() { { "0", new Item("0") } }, comparer);
            TestHelper.AssertNotEquivalent(new Dictionary<string, Item>() { { "0", new Item("0") } }, new Dictionary<string, Item>(), comparer);
            TestHelper.AssertNotEquivalent(new Dictionary<string, Item>() { { "0", new Item("0") } }, new Dictionary<string, Item>() { { "1", new Item("1") } }, comparer);
            TestHelper.AssertNotEquivalent(new Dictionary<string, Item>() { { "0", new Item("0") } }, new Dictionary<string, Item>() { { "0", new Item("0") }, { "1", new Item("1") } }, comparer);
        }
    }
}

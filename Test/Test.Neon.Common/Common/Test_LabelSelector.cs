//-----------------------------------------------------------------------------
// FILE:	    Test_LabelSelector.cs
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
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_LabelSelector
    {
        //---------------------------------------------------------------------
        // Private types

        public class TestItem : ILabeled
        {
            public TestItem(string name, params KeyValuePair<string, string>[] labels)
            {
                this.Name = name;

                foreach (var label in labels)
                {
                    Items.Add(label.Key, label.Value);
                }
            }

            public string Name { get; private set; }

            public Dictionary<string, string> Items { get; private set; } = new Dictionary<string, string>();

            public IDictionary<string, string> GetLabels() => Items;
        }

        public class TestItemCaseInsensitive : ILabeled
        {
            public TestItemCaseInsensitive(string name, params KeyValuePair<string, string>[] labels)
            {
                this.Name = name;

                foreach (var label in labels)
                {
                    Items.Add(label.Key, label.Value);
                }
            }

            public string Name { get; private set; }

            public Dictionary<string, string> Items { get; private set; } = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);

            public IDictionary<string, string> GetLabels() => Items;
        }

        //---------------------------------------------------------------------
        // Implementation

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Equal()
        {
            var items = new List<TestItem>();

            // Test NULL items

            var selector = new LabelSelector<TestItem>(null);

            Assert.Empty(selector.SelectItems("test == true"));
            Assert.Empty(selector.SelectItems("test = true"));
            Assert.Empty(selector.SelectItems("test==true"));
            Assert.Empty(selector.SelectItems("test=true"));

            // Test empty items

            selector = new LabelSelector<TestItem>(items);

            Assert.Empty(selector.SelectItems("test == true"));
            Assert.Empty(selector.SelectItems("test = true"));
            Assert.Empty(selector.SelectItems("test==true"));
            Assert.Empty(selector.SelectItems("test=true"));

            // Test selecting from a single item

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("test", "true"),
                new KeyValuePair<string, string>("cloud", "false"),
                new KeyValuePair<string, string>("onpremise", "true")));

            Assert.Empty(selector.SelectItems("test == false"));
            Assert.Single(selector.SelectItems("test == true"));
            Assert.Empty(selector.SelectItems("test = false"));
            Assert.Single(selector.SelectItems("test = true"));
            Assert.Empty(selector.SelectItems("test==false"));
            Assert.Single(selector.SelectItems("test==true"));
            Assert.Empty(selector.SelectItems("test=false"));
            Assert.Single(selector.SelectItems("test=true"));

            Assert.Equal("one", selector.SelectItems("test=true").First().Name);

            // Test selecting from a multiple items

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("test", "true"),
                new KeyValuePair<string, string>("cloud", "false"),
                new KeyValuePair<string, string>("onpremise", "true")));
            items.Add(new TestItem("two",
                new KeyValuePair<string, string>("test", "false"),
                new KeyValuePair<string, string>("cloud", "true"),
                new KeyValuePair<string, string>("onpremise", "true")));

            Assert.Single(selector.SelectItems("test == false"));
            Assert.Single(selector.SelectItems("test = false"));
            Assert.Single(selector.SelectItems("test==false"));
            Assert.Single(selector.SelectItems("test=false"));

            Assert.Equal("two", selector.SelectItems("test=false").First().Name);
        }
    }
}

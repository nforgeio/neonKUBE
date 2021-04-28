//-----------------------------------------------------------------------------
// FILE:	    Test_LabelSelector.cs
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
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void Equal()
        {
            LabelSelector<TestItem> selector;

            var items = new List<TestItem>();

            // Test NULL items

            selector = new LabelSelector<TestItem>(items);

            Assert.Empty(selector.Select("test == true"));
            Assert.Empty(selector.Select("test = true"));
            Assert.Empty(selector.Select("\ttest\r\n==true"));
            Assert.Empty(selector.Select("test=true"));

            // Test empty items

            selector = new LabelSelector<TestItem>(items);

            Assert.Empty(selector.Select("test == true"));
            Assert.Empty(selector.Select("test = true"));
            Assert.Empty(selector.Select("test==true"));
            Assert.Empty(selector.Select("test=true"));

            // Test selecting from a single item

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("test", "true"),
                new KeyValuePair<string, string>("cloud", "false"),
                new KeyValuePair<string, string>("onpremise", "true")));

            selector = new LabelSelector<TestItem>(items);

            Assert.Empty(selector.Select("test == false"));
            Assert.Single(selector.Select("test == true"));
            Assert.Empty(selector.Select("test = false"));
            Assert.Single(selector.Select("test = true"));
            Assert.Empty(selector.Select("test==false"));
            Assert.Single(selector.Select("test==true"));
            Assert.Empty(selector.Select("test=false"));
            Assert.Single(selector.Select("test=true"));

            Assert.Equal("one", selector.Select("test=true").First().Name);

            // Test selecting from a multiple items

            selector = new LabelSelector<TestItem>(items);

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("test", "true"),
                new KeyValuePair<string, string>("cloud", "false"),
                new KeyValuePair<string, string>("onpremise", "true")));
            items.Add(new TestItem("two",
                new KeyValuePair<string, string>("test", "false"),
                new KeyValuePair<string, string>("cloud", "true"),
                new KeyValuePair<string, string>("onpremise", "true")));

            Assert.Single(selector.Select("test == false"));
            Assert.Single(selector.Select("test = false"));
            Assert.Single(selector.Select("test==false"));
            Assert.Single(selector.Select("test=false"));

            Assert.Equal("one", selector.Select("test=true").First().Name);
            Assert.Equal("two", selector.Select("test=false").First().Name);
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void NotEqual()
        {
            LabelSelector<TestItem> selector;

            var items = new List<TestItem>();

            // Test NULL items

            selector = new LabelSelector<TestItem>(items);

            Assert.Empty(selector.Select("test != true"));
            Assert.Empty(selector.Select("\ttest\r\n!=true"));
            Assert.Empty(selector.Select("test!=true"));

            // Test empty items

            selector = new LabelSelector<TestItem>(items);

            Assert.Empty(selector.Select("test != true"));
            Assert.Empty(selector.Select("test!=true"));
            Assert.Empty(selector.Select("test!=true"));

            // Test selecting from a single item

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("test", "true"),
                new KeyValuePair<string, string>("cloud", "false"),
                new KeyValuePair<string, string>("onpremise", "true")));

            selector = new LabelSelector<TestItem>(items);

            Assert.Single(selector.Select("test != false"));
            Assert.Empty(selector.Select("test != true"));
            Assert.Single(selector.Select("test!=false"));
            Assert.Empty(selector.Select("test!=true"));

            Assert.Equal("one", selector.Select("test=true").First().Name);

            // Test selecting from a multiple items

            selector = new LabelSelector<TestItem>(items);

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("test", "true"),
                new KeyValuePair<string, string>("cloud", "false"),
                new KeyValuePair<string, string>("onpremise", "true")));
            items.Add(new TestItem("two",
                new KeyValuePair<string, string>("test", "false"),
                new KeyValuePair<string, string>("cloud", "true"),
                new KeyValuePair<string, string>("onpremise", "true")));

            Assert.Single(selector.Select("test != true"));
            Assert.Single(selector.Select("test!=true"));

            Assert.Equal("one", selector.Select("test!=false").First().Name);
            Assert.Equal("two", selector.Select("test!=true").First().Name);
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void Has()
        {
            LabelSelector<TestItem> selector;

            var items = new List<TestItem>();

            // Test NULL items

            selector = new LabelSelector<TestItem>(items);

            Assert.Empty(selector.Select("test"));
            Assert.Empty(selector.Select("\ttest\r\n"));

            // Test empty items

            selector = new LabelSelector<TestItem>(items);

            Assert.Empty(selector.Select("test"));
            Assert.Empty(selector.Select("\ttest\r\n"));

            // Test selecting from a single item

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("test", "true"),
                new KeyValuePair<string, string>("cloud", "false"),
                new KeyValuePair<string, string>("onpremise", "true")));

            selector = new LabelSelector<TestItem>(items);

            Assert.Empty(selector.Select("foo"));
            Assert.Single(selector.Select("onpremise"));

            // Test selecting from a multiple items

            selector = new LabelSelector<TestItem>(items);

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("test", "true"),
                new KeyValuePair<string, string>("cloud", "false"),
                new KeyValuePair<string, string>("onpremise", "true")));
            items.Add(new TestItem("two",
                new KeyValuePair<string, string>("test", "false"),
                new KeyValuePair<string, string>("cloud", "true"),
                new KeyValuePair<string, string>("foobar", "true"),
                new KeyValuePair<string, string>("onpremise", "true")));

            Assert.Empty(selector.Select("foo"));
            Assert.Single(selector.Select("foobar"));
            Assert.Equal(2, selector.Select("onpremise").Count());
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void NotHas()
        {
            LabelSelector<TestItem> selector;

            var items = new List<TestItem>();

            // Test NULL items

            selector = new LabelSelector<TestItem>(items);

            Assert.Empty(selector.Select("!test"));
            Assert.Empty(selector.Select("! test"));
            Assert.Empty(selector.Select("\t!test\r\n"));

            // Test empty items

            selector = new LabelSelector<TestItem>(items);

            Assert.Empty(selector.Select("!test"));
            Assert.Empty(selector.Select("! test"));
            Assert.Empty(selector.Select("\t!test\r\n"));

            // Test selecting from a single item

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("test", "true"),
                new KeyValuePair<string, string>("cloud", "false"),
                new KeyValuePair<string, string>("onpremise", "true")));

            selector = new LabelSelector<TestItem>(items);

            Assert.Single(selector.Select("!foo"));
            Assert.Empty(selector.Select("!onpremise"));

            // Test selecting from a multiple items

            selector = new LabelSelector<TestItem>(items);

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("test", "true"),
                new KeyValuePair<string, string>("cloud", "false"),
                new KeyValuePair<string, string>("onpremise", "true")));
            items.Add(new TestItem("two",
                new KeyValuePair<string, string>("test", "false"),
                new KeyValuePair<string, string>("cloud", "true"),
                new KeyValuePair<string, string>("foobar", "true"),
                new KeyValuePair<string, string>("onpremise", "true")));

            Assert.Equal(2, selector.Select("!foo").Count());
            Assert.Single(selector.Select("!foobar"));
            Assert.Empty(selector.Select("!onpremise"));
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void In()
        {
            LabelSelector<TestItem> selector;

            var items = new List<TestItem>();

            // Test NULL items

            selector = new LabelSelector<TestItem>(items);

            Assert.Empty(selector.Select("test in (aaa)"));
            Assert.Empty(selector.Select("test in\t\r\n(aaa)"));

            // Test empty items

            selector = new LabelSelector<TestItem>(items);

            Assert.Empty(selector.Select("test in (aaa)"));
            Assert.Empty(selector.Select("test in\t\r\n(aaa)"));

            // Test selecting from a single item

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("test", "aa"),
                new KeyValuePair<string, string>("cloud", "false"),
                new KeyValuePair<string, string>("onpremise", "true")));

            selector = new LabelSelector<TestItem>(items);

            Assert.Single(selector.Select("test in (aa)"));
            Assert.Empty(selector.Select("test in (bb)"));

            // Test selecting from a multiple items

            selector = new LabelSelector<TestItem>(items);

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("test", "aa"),
                new KeyValuePair<string, string>("cloud", "false"),
                new KeyValuePair<string, string>("onpremise", "true")));
            items.Add(new TestItem("two",
                new KeyValuePair<string, string>("test", "bb"),
                new KeyValuePair<string, string>("cloud", "true"),
                new KeyValuePair<string, string>("foobar", "true"),
                new KeyValuePair<string, string>("onpremise", "true")));

            Assert.Single(selector.Select("test in (aa)"));
            Assert.Equal(2, selector.Select("test in (aa, bb)").Count());
            Assert.Empty(selector.Select("test in (cc)"));
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void NotIn()
        {
            LabelSelector<TestItem> selector;

            var items = new List<TestItem>();

            // Test NULL items

            selector = new LabelSelector<TestItem>(items);

            Assert.Empty(selector.Select("test in (aaa)"));
            Assert.Empty(selector.Select("test in\t\r\n(aaa)"));

            // Test empty items

            selector = new LabelSelector<TestItem>(items);

            Assert.Empty(selector.Select("test in (aaa)"));
            Assert.Empty(selector.Select("test in\t\r\n(aaa)"));

            // Test selecting from a single item

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("test", "aa"),
                new KeyValuePair<string, string>("cloud", "false"),
                new KeyValuePair<string, string>("onpremise", "true")));

            selector = new LabelSelector<TestItem>(items);

            Assert.Single(selector.Select("test notin (cc)"));
            Assert.Empty(selector.Select("test notin (aa)"));

            // Test selecting from a multiple items

            selector = new LabelSelector<TestItem>(items);

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("test", "aa"),
                new KeyValuePair<string, string>("cloud", "false"),
                new KeyValuePair<string, string>("onpremise", "true")));
            items.Add(new TestItem("two",
                new KeyValuePair<string, string>("test", "bb"),
                new KeyValuePair<string, string>("cloud", "true"),
                new KeyValuePair<string, string>("foobar", "true"),
                new KeyValuePair<string, string>("onpremise", "true")));

            Assert.Single(selector.Select("test notin (aa)"));
            Assert.Equal(2, selector.Select("test notin (cc, dd)").Count());
            Assert.Empty(selector.Select("test notin (aa, bb)"));
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void Multiple()
        {
            LabelSelector<TestItem> selector;

            var items = new List<TestItem>();

            // Test multple selector conditions.

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("test", "aa"),
                new KeyValuePair<string, string>("cloud", "false"),
                new KeyValuePair<string, string>("onpremise", "true")));
            items.Add(new TestItem("two",
                new KeyValuePair<string, string>("test", "bb"),
                new KeyValuePair<string, string>("cloud", "true"),
                new KeyValuePair<string, string>("foobar", "true"),
                new KeyValuePair<string, string>("onpremise", "true")));

            selector = new LabelSelector<TestItem>(items);

            Assert.Empty(selector.Select("test==aa,test=foo"));
            Assert.Single(selector.Select("test==aa,onpremise=true"));
            Assert.Equal(2, selector.Select("test in (aa, bb),onpremise=true").Count());
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void CaseInsensitive_Labels()
        {
            LabelSelector<TestItemCaseInsensitive> selector;

            var items = new List<TestItemCaseInsensitive>();

            selector = new LabelSelector<TestItemCaseInsensitive>(items);

            items.Clear();
            items.Add(new TestItemCaseInsensitive("one",
                new KeyValuePair<string, string>("test", "true"),
                new KeyValuePair<string, string>("cloud", "false"),
                new KeyValuePair<string, string>("onpremise", "true")));
            items.Add(new TestItemCaseInsensitive("two",
                new KeyValuePair<string, string>("TEST", "false"),
                new KeyValuePair<string, string>("CLOUD", "true"),
                new KeyValuePair<string, string>("ONPREMISE", "true")));

            Assert.Single(selector.Select("test == false"));
            Assert.Single(selector.Select("TEST == false"));
            Assert.Equal(2, selector.Select("test in (true,false)").Count());
            Assert.Equal(2, selector.Select("TEST in (true,false)").Count());
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void CaseInsensitive_Values()
        {
            LabelSelector<TestItem> selector;

            var items = new List<TestItem>();

            selector = new LabelSelector<TestItem>(items, LabelSelectorOptions.CaseInsensitiveValues);

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("test", "true"),
                new KeyValuePair<string, string>("cloud", "false"),
                new KeyValuePair<string, string>("onpremise", "true")));
            items.Add(new TestItem("two",
                new KeyValuePair<string, string>("test", "FALSE"),
                new KeyValuePair<string, string>("cloud", "TRUE"),
                new KeyValuePair<string, string>("onpremise", "TRUE")));

            Assert.Single(selector.Select("test == false"));
            Assert.Single(selector.Select("test == FALSE"));
            Assert.Equal(2, selector.Select("test in (TRUE,false)").Count());
            Assert.Equal(2, selector.Select("test in (true,FALSE)").Count());
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void Constraints_Enabled()
        {
            LabelSelector<TestItem> selector;

            var items = new List<TestItem>();

            selector = new LabelSelector<TestItem>(items);

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("neonkube.com/test", "true")));
            items.Add(new TestItem("two",
                new KeyValuePair<string, string>("neonkube.com/test", "false")));

            Assert.Single(selector.Select("neonkube.com/test == false"));

            Assert.Equal("one", selector.Select("neonkube.com/test=true").First().Name);
            Assert.Equal("two", selector.Select("neonkube.com/test=false").First().Name);

            // Verify that we can various kinds of invalid label keys.

            Assert.Throws<FormatException>(() => selector.Select("/==true"));
            Assert.Throws<FormatException>(() => selector.Select("/test==true"));
            Assert.Throws<FormatException>(() => selector.Select("com/test==true"));
            Assert.Throws<FormatException>(() => selector.Select("*test.com/test==true"));
            Assert.Throws<FormatException>(() => selector.Select("test.com*/test==true"));

            var maxDnsLabel = new string('a', 63);
            var maxName     = new string('z', 63);
            var maxValue    = new string('b', 63);
            var maxDnsName  = new string('c', 63) + '.' + new string('d', 63) + '.' + new string('e', 63) + '.' + new string('f', 61);

            Assert.Equal(253, maxDnsName.Length);

            // Verify label keys

            selector.Select($"{maxDnsName}/test==true");                                                    // This is OK
            selector.Select($"a.b.c/test==true");                                                           // This is OK
            selector.Select($"0.test-foo.com/test==true");                                                  // This is OK
            selector.Select($"0.test_foo.com/test==true");                                                  // This is OK
            selector.Select($"0.test_foo.com/{maxName}==true");                                             // This is OK
            Assert.Throws<FormatException>(() => selector.Select($"_test.com/test==true"));                 // Invalid first character
            Assert.Throws<FormatException>(() => selector.Select($"test.com_/test==true"));                 // Invalid last character
            Assert.Throws<FormatException>(() => selector.Select($"{maxDnsName}g/test==true"));             // DNS name too long
            Assert.Throws<FormatException>(() => selector.Select($"a{maxDnsName}/test==true"));             // DNS label too long
            Assert.Throws<FormatException>(() => selector.Select($"com/test==true"));                       // Not enough DNS labels
            Assert.Throws<FormatException>(() => selector.Select($"test..com/test==true"));                 // Missing DNS label
            Assert.Throws<FormatException>(() => selector.Select($"0.test_foo.com/{maxName}q==true"));      // Name part is too long
            
            // Verify label values

            selector.Select($"foo.com/test=={maxValue}");                                                   // This is OK
            selector.Select($"foo.com/test==0123456789_abcdefghijklmnopqrstuvwxyz.test_test");              // This is OK
            selector.Select($"foo.com/test==0123456789_ABCDEFGHIJKLMNOPQRSTUVWXYZ.TEST_TEST");              // This is OK
            selector.Select($"foo.com/test=={maxValue}");                                                   // This is OK
            Assert.Throws<FormatException>(() => selector.Select($"foo.com/test=={maxValue}g"));            // Value is too long
            Assert.Throws<FormatException>(() => selector.Select($"foo.com/test==*"));                      // Value has invalid character
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void Constraints_Disabled()
        {
            LabelSelector<TestItem> selector;

            var items = new List<TestItem>();

            // Verify selectors with valid Kubernetes label keys still works in unconstrained mode. 

            selector = new LabelSelector<TestItem>(items, LabelSelectorOptions.UnConstraintedLabels);

            items.Clear();
            items.Add(new TestItem("one",
                new KeyValuePair<string, string>("neonKUBE.com/test", "true")));
            items.Add(new TestItem("two",
                new KeyValuePair<string, string>("neonKUBE.com/test", "false")));

            Assert.Single(selector.Select("neonKUBE.com/test == false"));

            Assert.Equal("one", selector.Select("neonKUBE.com/test=true").First().Name);
            Assert.Equal("two", selector.Select("neonKUBE.com/test=false").First().Name);

            var maxDnsLabel = new string('a', 63);
            var maxName     = new string('z', 63);
            var maxValue    = new string('b', 63);
            var maxDnsName  = new string('c', 63) + '.' + new string('d', 63) + '.' + new string('e', 63) + '.' + new string('f', 61);

            Assert.Equal(253, maxDnsName.Length);

            // Verify label keys

            selector.Select($"{maxDnsName}/test==true");                                                        // This is OK
            selector.Select($"a.b.c/test==true");                                                               // This is OK
            selector.Select($"0.test-foo.com/test==true");                                                      // This is OK
            selector.Select($"0.test_foo.com/test==true");                                                      // This is OK
            selector.Select($"0.test_foo.com/{maxName}==true");                                                 // This is OK
            selector.Select($"_test.com/test==true");                                                           // Invalid first character
            selector.Select($"test.com_/test==true");                                                           // Invalid last character
            selector.Select($"{maxDnsName}g/test==true");                                                       // DNS name too long
            selector.Select($"a{maxDnsName}/test==true");                                                       // DNS label too long
            selector.Select($"com/test==true");                                                                 // Not enough DNS labels
            selector.Select($"test..com/test==true");                                                           // Missing DNS label
            selector.Select($"0.test_foo.com/{maxName}q==true");                                                // Name part is too long

            // Verify label values

            selector.Select($"foo.com/test=={maxValue}");                                                       // This is OK
            selector.Select($"foo.com/test==0123456789_abcdefghijklmnopqrstuvwxyz.test_test");                  // This is OK
            selector.Select($"foo.com/test==0123456789_ABCDEFGHIJKLMNOPQRSTUVWXYZ.TEST_TEST");                  // This is OK
            selector.Select($"foo.com/test=={maxValue}");                                                       // This is OK
            selector.Select($"foo.com/test=={maxValue}g");                                                      // Value is too long
            selector.Select($"foo.com/test==*");                                                                // Value has invalid character
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void Constraints_Explicit()
        {
            // Verify that explicit Kubernetes client label checks work.

            var maxDnsLabel = new string('a', 63);
            var maxName     = new string('z', 63);
            var maxValue    = new string('b', 63);
            var maxDnsName  = new string('c', 63) + '.' + new string('d', 63) + '.' + new string('e', 63) + '.' + new string('f', 61);

            Assert.Equal(253, maxDnsName.Length);

            // Verify label keys

            LabelSelector.ValidateLabelKey($"{maxDnsName}/test");                                               // This is OK
            LabelSelector.ValidateLabelKey($"a.b.c/test");                                                      // This is OK
            LabelSelector.ValidateLabelKey($"0.test-foo.com/test");                                             // This is OK
            LabelSelector.ValidateLabelKey($"0.test_foo.com/test");                                             // This is OK
            LabelSelector.ValidateLabelKey($"0.test_foo.com/{maxName}");                                        // This is OK
            Assert.Throws<FormatException>(() => LabelSelector.ValidateLabelKey($"_test.com/test"));            // Invalid first prefix character
            Assert.Throws<FormatException>(() => LabelSelector.ValidateLabelKey($"test.com_/test"));            // Invalid last prefix character
            Assert.Throws<FormatException>(() => LabelSelector.ValidateLabelKey($"{maxDnsName}g/test"));        // Prefix too long
            Assert.Throws<FormatException>(() => LabelSelector.ValidateLabelKey($"a{maxDnsName}/test"));        // DNS label too long
            Assert.Throws<FormatException>(() => LabelSelector.ValidateLabelKey($"com/test"));                  // Not enough DNS labels
            Assert.Throws<FormatException>(() => LabelSelector.ValidateLabelKey($"test..com/test"));            // Missing DNS label
            Assert.Throws<FormatException>(() => LabelSelector.ValidateLabelKey($"0.test_foo.com/{maxName}q")); // Name part is too long

            // Verify label values

            LabelSelector.ValidateLabelValue($"{maxValue}");                                                    // This is OK
            LabelSelector.ValidateLabelValue($"0123456789_abcdefghijklmnopqrstuvwxyz.test_test");               // This is OK
            LabelSelector.ValidateLabelValue($"f0123456789_ABCDEFGHIJKLMNOPQRSTUVWXYZ.TEST_TEST");              // This is OK
            Assert.Throws<FormatException>(() => LabelSelector.ValidateLabelValue($"{maxValue}g"));             // Value is too long
            Assert.Throws<FormatException>(() => LabelSelector.ValidateLabelValue($"*"));                       // Value has invalid character
        }
    }
}

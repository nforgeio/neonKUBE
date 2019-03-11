//-----------------------------------------------------------------------------
// FILE:	    Test_SerializedList.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.CodeGen;
using Neon.Common;
using Neon.Xunit;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

namespace TestCodeGen
{
    public class Test_SerializedList
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void Basic()
        {
            // Verify that all basic list operations still work.

            var jObject   = new JObject();
            var jProperty = new JProperty("test", null);

            jObject.Add(jProperty);

            var list = new SerializedList<string>(jProperty);

            Assert.Empty(list);
            Assert.False(list.IsReadOnly);

            list.Add("zero");
            list.Add("one");
            list.Add("two");
            list.Add("three");
            list.Add("four");
            Assert.Equal(5, list.Count);
            Assert.Equal(1, list.IndexOf("one"));

            var items = new List<string>();

            foreach (var item in list)
            {
                items.Add(item);
            }

            Assert.Equal(new string[] { "zero", "one", "two", "three", "four" }, items);

            list.RemoveAt(0);
            items.RemoveAt(0);
            Assert.Equal(items, list);

            list.Insert(0, "0");
            items.Insert(0, "0");
            Assert.Equal(items, list);

            list.Remove("0");
            items.Remove("0");
            Assert.Equal(items, list);

            list.Insert(0, "zero");
            items.Insert(0, "zero");
            Assert.Equal(items, list);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCodeGen)]
        public void Performance()
        {
            // Compare the performance of the SerializedList Add() operation
            // against the stock List class.

            var jObject = new JObject();
            var jProperty = new JProperty("test", null);

            jObject.Add(jProperty);

            var slist = new SerializedList<int>(jProperty);
            var list  = new List<int>();

            const int iterations      = 1000000;
            const int opsPerIteration = 4;      // Clear() + 3 * Add()

            var stopwatch = new Stopwatch();

            //---------------------------------------------
            // Measure: SerializedList

            stopwatch.Reset();
            stopwatch.Start();

            for (int i = 0; i < iterations; i++)
            {
                slist.Clear();

                for (int j = 0; j < 3; j++)
                {
                    slist.Add(j);
                }
            }

            stopwatch.Stop();

            var slistTime = stopwatch.Elapsed;

            //---------------------------------------------
            // Measure: List

            stopwatch.Reset();
            stopwatch.Start();

            for (int i = 0; i < iterations; i++)
            {
                list.Clear();

                for (int j = 0; j < 3; j++)
                {
                    list.Add(j);
                }
            }

            stopwatch.Stop();

            var listTime = stopwatch.Elapsed;

            //---------------------------------------------

            double slistOpsPerSecond = (iterations * opsPerIteration) / slistTime.TotalSeconds;
            double listOpsPerSecond  = (iterations * opsPerIteration) / listTime.TotalSeconds;
            double ratio             = slistOpsPerSecond / listOpsPerSecond;
            double invertedRatio     = 1.0 / ratio;
        }
    }
}

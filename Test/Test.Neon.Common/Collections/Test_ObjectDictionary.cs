//-----------------------------------------------------------------------------
// FILE:	    Test_ObjectDictionary.cs
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
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_ObjectDictionary
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Basic()
        {
            var dictionary = new ObjectDictionary();

            Assert.Throws<KeyNotFoundException>(() => dictionary.Get<string>("test"));
            Assert.Null(dictionary.Get<string>("test", null));
            Assert.Equal("default", dictionary.Get<string>("test", "default"));

            dictionary.Add("test-bool", true);
            Assert.True(dictionary.Get<bool>("test-bool"));
            Assert.Throws<InvalidCastException>(() => dictionary.Get<string>("test-bool"));
            Assert.True(dictionary.Get<bool>("does-not-exist", true));
            Assert.False(dictionary.Get<bool>("does-not-exist", false));

            dictionary.Add("test-int", 1234);
            Assert.Equal(1234, dictionary.Get<int>("test-int"));
            Assert.Throws<InvalidCastException>(() => dictionary.Get<string>("test-bool"));
            Assert.Equal(1234, dictionary.Get<int>("does-not-exist", 1234));
        }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    Test_KubeContextName.cs
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
using Neon.IO;
using Neon.Kube;
using Neon.Kube.Xunit;
using Neon.Xunit;

using Xunit;

namespace TestKube
{
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_KubeContextName
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public void Parse()
        {
            // Verify that the components are parsed correctly:

            var name = KubeContextName.Parse("user@cluster/namespace");

            Assert.Equal("user", name.User);
            Assert.Equal("cluster", name.Cluster);
            Assert.Equal("namespace", name.Namespace);

            name = KubeContextName.Parse("me@mine/my-space");

            Assert.Equal("me", name.User);
            Assert.Equal("mine", name.Cluster);
            Assert.Equal("my-space", name.Namespace);

            name = KubeContextName.Parse("user@cluster");

            Assert.Equal("user", name.User);
            Assert.Equal("cluster", name.Cluster);
            Assert.Equal("default", name.Namespace);

            // Ensure that the components are converted to lowercase.

            name = KubeContextName.Parse("USER@CLUSTER/NAMESPACE");

            Assert.Equal("user", name.User);
            Assert.Equal("cluster", name.Cluster);
            Assert.Equal("namespace", name.Namespace);

            name = new KubeContextName("USER", "CLUSTER", "NAMESPACE");

            Assert.Equal("user", name.User);
            Assert.Equal("cluster", name.Cluster);
            Assert.Equal("namespace", name.Namespace);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public void ParseError()
        {
            Assert.Throws<ArgumentNullException>(() => KubeContextName.Parse(null));
            Assert.Throws<ArgumentNullException>(() => KubeContextName.Parse(""));

            var tooLong = new string('a', 254);

            Assert.Throws<FormatException>(() => KubeContextName.Parse("name"));                         // Missing cluster
            Assert.Throws<FormatException>(() => KubeContextName.Parse("name/namespace"));               // Missing cluster
            Assert.Throws<FormatException>(() => KubeContextName.Parse($"{tooLong}@cluster/namespace")); // User is too long
            Assert.Throws<FormatException>(() => KubeContextName.Parse($"user@{tooLong}/namespace"));    // Cluster is too long
            Assert.Throws<FormatException>(() => KubeContextName.Parse($"user@cluster/{tooLong}"));      // Namespace is too long
            Assert.Throws<FormatException>(() => KubeContextName.Parse("user_name@cluster/namespace"));  // User has bad character
            Assert.Throws<FormatException>(() => KubeContextName.Parse("usercluster_name/namespace"));   // Cluster has bad character
            Assert.Throws<FormatException>(() => KubeContextName.Parse("user@cluster/namespace_name"));  // Namespace has bad character
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public void HashCode()
        {
            var hash1 = KubeContextName.Parse("user1@cluster/test").GetHashCode();
            var hash2 = KubeContextName.Parse("user2@cluster/test").GetHashCode();

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public void Equal()
        {
            Assert.True(KubeContextName.Parse("user@cluster").Equals(KubeContextName.Parse("user@cluster")));
            Assert.True(KubeContextName.Parse("user@cluster").Equals(KubeContextName.Parse("user@cluster/default")));

            Assert.False(KubeContextName.Parse("user@cluster/default").Equals(KubeContextName.Parse("FOO@cluster/default")));
            Assert.False(KubeContextName.Parse("user@cluster/default").Equals(KubeContextName.Parse("user@BAR/default")));
            Assert.False(KubeContextName.Parse("user@cluster/default").Equals(KubeContextName.Parse("user@cluster/FOOBAR")));
            Assert.False(KubeContextName.Parse("user@cluster/default").Equals(null));
            Assert.False(KubeContextName.Parse("user@cluster/default").Equals("not a config name type"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public void EqualOperator()
        {
            Assert.True(KubeContextName.Parse("user@cluster") == KubeContextName.Parse("user@cluster"));
            Assert.True(KubeContextName.Parse("user@cluster") == KubeContextName.Parse("user@cluster/default"));
            Assert.True((KubeContextName)null == (KubeContextName)null);

            Assert.False(KubeContextName.Parse("user@cluster/test") == KubeContextName.Parse("foo@cluster/test"));
            Assert.False(KubeContextName.Parse("user@cluster/test") == KubeContextName.Parse("user@bar/test"));
            Assert.False(KubeContextName.Parse("user@cluster/test") == KubeContextName.Parse("user@cluster/foobar"));
            Assert.False(KubeContextName.Parse("user@cluster/test") == (KubeContextName)null);
            Assert.False((KubeContextName)null == KubeContextName.Parse("user@cluster/foobar"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public void NotEqualOperator()
        {
            Assert.False(KubeContextName.Parse("user@cluster") != KubeContextName.Parse("user@cluster"));
            Assert.False(KubeContextName.Parse("user@cluster") != KubeContextName.Parse("user@cluster/default"));
            Assert.False((KubeContextName)null != (KubeContextName)null);

            Assert.True(KubeContextName.Parse("user@cluster/test") != KubeContextName.Parse("foo@cluster/test"));
            Assert.True(KubeContextName.Parse("user@cluster/test") != KubeContextName.Parse("user@bar/test"));
            Assert.True(KubeContextName.Parse("user@cluster/test") != KubeContextName.Parse("user@cluster/foobar"));
            Assert.True(KubeContextName.Parse("user@cluster/test") != (KubeContextName)null);
            Assert.True((KubeContextName)null != KubeContextName.Parse("user@cluster/foobar"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public void Cast()
        {
            // string --> KubeContextName:

            Assert.Null((KubeContextName)(string)null);
            Assert.Null((KubeContextName)string.Empty);

            var context = (KubeContextName)"user@cluster/namespace";

            Assert.Equal("user", context.User);
            Assert.Equal("cluster", context.Cluster);
            Assert.Equal("namespace", context.Namespace);

            // KubeContextName --> string:

            Assert.Null((string)(KubeContextName)null);
            Assert.Equal("user@cluster/namespace", (string)context);
        }
    }
}

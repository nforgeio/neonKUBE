//-----------------------------------------------------------------------------
// FILE:	    Test_KubeContextName.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright © 2005-2023 by NEONFORGE LLC.  All rights reserved.
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
using Neon.Kube.Config;
using Neon.Kube.Xunit;
using Neon.Xunit;

using Xunit;

namespace TestKube
{
    [Trait(TestTrait.Category, TestArea.NeonKube)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public class Test_KubeContextName
    {
        [Fact]
        public void ParseNeonKube()
        {
            // Verify that the NEONKUBE components are parsed correctly.

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
        }

        [Fact]
        public void ParseStandard()
        {
            // Verify that the standard components are parsed correctly (without the "USER@").

            var name = KubeContextName.Parse("cluster/namespace");

            Assert.Null(name.User);
            Assert.Equal("cluster", name.Cluster);
            Assert.Equal("namespace", name.Namespace);

            name = KubeContextName.Parse("mine/my-space");

            Assert.Null(name.User);
            Assert.Equal("mine", name.Cluster);
            Assert.Equal("my-space", name.Namespace);

            name = KubeContextName.Parse("cluster");

            Assert.Null(name.User);
            Assert.Equal("cluster", name.Cluster);
            Assert.Equal("default", name.Namespace);
        }

        [Fact]
        public void ParseStandard_WithError()
        {
            Assert.Throws<FormatException>(() => KubeContextName.Parse(null));
            Assert.Throws<FormatException>(() => KubeContextName.Parse(""));

            var tooLong = new string('a', 254);

            Assert.Throws<FormatException>(() => KubeContextName.Parse("user@"));                       // Missing cluster
            Assert.Throws<FormatException>(() => KubeContextName.Parse("user@/namespace"));             // Missing cluster
            Assert.Throws<FormatException>(() => KubeContextName.Parse($"user@{tooLong}/namespace"));   // Cluster is too long
            Assert.Throws<FormatException>(() => KubeContextName.Parse($"user@cluster/{tooLong}"));     // WatchNamespace is too long
            Assert.Throws<FormatException>(() => KubeContextName.Parse("user@cluster_name/namespace")); // Cluster has bad character
            Assert.Throws<FormatException>(() => KubeContextName.Parse("user@cluster/namespace_name")); // WatchNamespace has bad character
        }

        [Fact]
        public void ParseNeonKube_WithError()
        {
            Assert.Throws<FormatException>(() => KubeContextName.Parse(null));
            Assert.Throws<FormatException>(() => KubeContextName.Parse(""));

            var tooLong = new string('a', 254);

            Assert.Throws<FormatException>(() => KubeContextName.Parse(""));                            // Missing cluster
            Assert.Throws<FormatException>(() => KubeContextName.Parse("/namespace"));                  // Missing cluster
            Assert.Throws<FormatException>(() => KubeContextName.Parse($"{tooLong}/namespace"));        // Cluster is too long
            Assert.Throws<FormatException>(() => KubeContextName.Parse($"cluster/{tooLong}"));          // WatchNamespace is too long
            Assert.Throws<FormatException>(() => KubeContextName.Parse("cluster_name/namespace"));      // Cluster has bad character
            Assert.Throws<FormatException>(() => KubeContextName.Parse("cluster/namespace_name"));      // WatchNamespace has bad character
        }

        [Fact]
        public void HashCode_NeonKube()
        {
            var hash1 = KubeContextName.Parse("user1@cluster/test").GetHashCode();
            var hash2 = KubeContextName.Parse("user2@cluster/test").GetHashCode();

            Assert.NotEqual(hash1, hash2);

            hash1 = KubeContextName.Parse("user@cluster1/test").GetHashCode();
            hash2 = KubeContextName.Parse("user@cluster2/test").GetHashCode();

            Assert.NotEqual(hash1, hash2);

            hash1 = KubeContextName.Parse("user@cluster/test1").GetHashCode();
            hash2 = KubeContextName.Parse("user@cluster/test2").GetHashCode();

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void HashCode_Standard()
        {
            var hash1 = KubeContextName.Parse("cluster1/test").GetHashCode();
            var hash2 = KubeContextName.Parse("cluster2/test").GetHashCode();

            Assert.NotEqual(hash1, hash2);

            hash1 = KubeContextName.Parse("cluster/test1").GetHashCode();
            hash2 = KubeContextName.Parse("cluster/test2").GetHashCode();

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        public void Equal_NeonKube()
        {
            Assert.True(KubeContextName.Parse("user@cluster").Equals(KubeContextName.Parse("user@cluster")));
            Assert.True(KubeContextName.Parse("user@cluster").Equals(KubeContextName.Parse("user@cluster/default")));
            Assert.True(KubeContextName.Parse("user@cluster/default").Equals(KubeContextName.Parse("USER@CLUSTER/DEFAULT")));

            Assert.False(KubeContextName.Parse("user@cluster/default").Equals(KubeContextName.Parse("FOO@cluster/default")));
            Assert.False(KubeContextName.Parse("user@cluster/default").Equals(KubeContextName.Parse("user@BAR/default")));
            Assert.False(KubeContextName.Parse("user@cluster/default").Equals(KubeContextName.Parse("user@cluster/FOOBAR")));
            Assert.False(KubeContextName.Parse("user@cluster/default").Equals(null));
            Assert.False(KubeContextName.Parse("user@cluster/default").Equals("not a config name type"));
        }

        [Fact]
        public void Equal_Standard()
        {
            Assert.True(KubeContextName.Parse("cluster").Equals(KubeContextName.Parse("cluster")));
            Assert.True(KubeContextName.Parse("cluster").Equals(KubeContextName.Parse("cluster/default")));
            Assert.True(KubeContextName.Parse("cluster/default").Equals(KubeContextName.Parse("CLUSTER/DEFAULT")));

            Assert.False(KubeContextName.Parse("cluster/default").Equals(KubeContextName.Parse("BAR/default")));
            Assert.False(KubeContextName.Parse("cluster/default").Equals(KubeContextName.Parse("cluster/FOOBAR")));
            Assert.False(KubeContextName.Parse("cluster/default").Equals(null));
            Assert.False(KubeContextName.Parse("cluster/default").Equals("not a config name type"));
        }

        [Fact]
        public void EqualOperator_NeonKube()
        {
            Assert.True(KubeContextName.Parse("user@cluster") == KubeContextName.Parse("user@cluster"));
            Assert.True(KubeContextName.Parse("user@cluster") == KubeContextName.Parse("user@cluster/default"));
            Assert.True((KubeContextName)null == (KubeContextName)null);

            Assert.False(KubeContextName.Parse("user@cluster/test") == KubeContextName.Parse("foo@cluster/test"));
            Assert.False(KubeContextName.Parse("user@cluster/test") == KubeContextName.Parse("user@bar/test"));
            Assert.False(KubeContextName.Parse("user@cluster/test") == KubeContextName.Parse("user@cluster/foobar"));
            Assert.False(KubeContextName.Parse("user@cluster/test") == KubeContextName.Parse("cluster/foobar"));
            Assert.False(KubeContextName.Parse("user@cluster/test") == (KubeContextName)null);
            Assert.False((KubeContextName)null == KubeContextName.Parse("user@cluster/foobar"));
        }

        [Fact]
        public void EqualOperator_Standard()
        {
            Assert.True(KubeContextName.Parse("cluster") == KubeContextName.Parse("cluster"));
            Assert.True(KubeContextName.Parse("cluster") == KubeContextName.Parse("cluster/default"));
            Assert.True((KubeContextName)null == (KubeContextName)null);

            Assert.False(KubeContextName.Parse("cluster/test") == KubeContextName.Parse("bar/test"));
            Assert.False(KubeContextName.Parse("cluster/test") == KubeContextName.Parse("cluster/foobar"));
            Assert.False(KubeContextName.Parse("cluster/test") == (KubeContextName)null);
            Assert.False((KubeContextName)null == KubeContextName.Parse("cluster/foobar"));
        }

        [Fact]
        public void NotEqualOperator_NeonKube()
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
        public void Cast_NeonKube()
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

        [Fact]
        public void Cast_Standard()
        {
            // string --> KubeContextName:

            Assert.Null((KubeContextName)(string)null);
            Assert.Null((KubeContextName)string.Empty);

            var context = (KubeContextName)"cluster/namespace";

            Assert.Null(context.User);
            Assert.Equal("cluster", context.Cluster);
            Assert.Equal("namespace", context.Namespace);

            // KubeContextName --> string:

            Assert.Null((string)(KubeContextName)null);
            Assert.Equal("cluster/namespace", (string)context);
        }

        [Fact]
        public void ToString_NeonKube()
        {
            Assert.Equal("user@context/namespace", KubeContextName.Parse("user@context/namespace").ToString());
            Assert.Equal("user@context/test", KubeContextName.Parse("user@context/TEST").ToString());
            Assert.Equal("user@context", KubeContextName.Parse("user@context/default").ToString());
            Assert.Equal("user@context", KubeContextName.Parse("user@context/DEFAULT").ToString());
        }

        [Fact]
        public void ToString_Standard()
        {
            Assert.Equal("context/namespace", KubeContextName.Parse("context/namespace").ToString());
            Assert.Equal("context/test", KubeContextName.Parse("context/TEST").ToString());
            Assert.Equal("context", KubeContextName.Parse("context/default").ToString());
            Assert.Equal("context", KubeContextName.Parse("context/DEFAULT").ToString());
        }
    }
}

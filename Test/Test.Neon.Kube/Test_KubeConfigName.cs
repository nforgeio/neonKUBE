//-----------------------------------------------------------------------------
// FILE:	    Test_KubeConfigName.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Kube;

using Xunit;

namespace TestHive
{
    public class Test_KubeConfigName
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public void Parse()
        {
            // Verify that the components are parsed correctly:

            var name = KubeConfigName.Parse("user@cluster/namespace");

            Assert.Equal("user", name.User);
            Assert.Equal("cluster", name.Cluster);
            Assert.Equal("namespace", name.Namespace);

            name = KubeConfigName.Parse("me@mine/my-space");

            Assert.Equal("me", name.User);
            Assert.Equal("mine", name.Cluster);
            Assert.Equal("my-space", name.Namespace);

            name = KubeConfigName.Parse("user@cluster");

            Assert.Equal("user", name.User);
            Assert.Equal("cluster", name.Cluster);
            Assert.Equal("default", name.Namespace);

            // Ensure that the components are converted to lowercase.

            name = KubeConfigName.Parse("USER@CLUSTER/NAMESPACE");

            Assert.Equal("user", name.User);
            Assert.Equal("cluster", name.Cluster);
            Assert.Equal("namespace", name.Namespace);

            name = new KubeConfigName("USER", "CLUSTER", "NAMESPACE");

            Assert.Equal("user", name.User);
            Assert.Equal("cluster", name.Cluster);
            Assert.Equal("namespace", name.Namespace);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public void ParseError()
        {
            Assert.Throws<ArgumentNullException>(() => KubeConfigName.Parse(null));
            Assert.Throws<ArgumentNullException>(() => KubeConfigName.Parse(""));

            var tooLong = new string('a', 254);

            Assert.Throws<FormatException>(() => KubeConfigName.Parse("name"));                         // Missing cluster
            Assert.Throws<FormatException>(() => KubeConfigName.Parse("name/namespace"));               // Missing cluster
            Assert.Throws<FormatException>(() => KubeConfigName.Parse($"{tooLong}@cluster/namespace")); // User is too long
            Assert.Throws<FormatException>(() => KubeConfigName.Parse($"user@{tooLong}/namespace"));    // Cluster is too long
            Assert.Throws<FormatException>(() => KubeConfigName.Parse($"user@cluster/{tooLong}"));      // Namespace is too long
            Assert.Throws<FormatException>(() => KubeConfigName.Parse("user_name@cluster/namespace"));  // User has bad character
            Assert.Throws<FormatException>(() => KubeConfigName.Parse("usercluster_name/namespace"));   // Cluster has bad character
            Assert.Throws<FormatException>(() => KubeConfigName.Parse("user@cluster/namespace_name"));  // Namespace has bad character
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public void HashCode()
        {
            var hash1 = KubeConfigName.Parse("user1@cluster/test").GetHashCode();
            var hash2 = KubeConfigName.Parse("user2@cluster/test").GetHashCode();

            Assert.NotEqual(hash1, hash2);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public void Equal()
        {
            Assert.True(KubeConfigName.Parse("user@cluster").Equals(KubeConfigName.Parse("user@cluster")));
            Assert.True(KubeConfigName.Parse("user@cluster").Equals(KubeConfigName.Parse("user@cluster/default")));

            Assert.False(KubeConfigName.Parse("user@cluster/default").Equals(KubeConfigName.Parse("FOO@cluster/default")));
            Assert.False(KubeConfigName.Parse("user@cluster/default").Equals(KubeConfigName.Parse("user@BAR/default")));
            Assert.False(KubeConfigName.Parse("user@cluster/default").Equals(KubeConfigName.Parse("user@cluster/FOOBAR")));
            Assert.False(KubeConfigName.Parse("user@cluster/default").Equals(null));
            Assert.False(KubeConfigName.Parse("user@cluster/default").Equals("not a config name type"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public void EqualOperator()
        {
            Assert.True(KubeConfigName.Parse("user@cluster") == KubeConfigName.Parse("user@cluster"));
            Assert.True(KubeConfigName.Parse("user@cluster") == KubeConfigName.Parse("user@cluster/default"));
            Assert.True((KubeConfigName)null == (KubeConfigName)null);

            Assert.False(KubeConfigName.Parse("user@cluster/test") == KubeConfigName.Parse("foo@cluster/test"));
            Assert.False(KubeConfigName.Parse("user@cluster/test") == KubeConfigName.Parse("user@bar/test"));
            Assert.False(KubeConfigName.Parse("user@cluster/test") == KubeConfigName.Parse("user@cluster/foobar"));
            Assert.False(KubeConfigName.Parse("user@cluster/test") == (KubeConfigName)null);
            Assert.False((KubeConfigName)null == KubeConfigName.Parse("user@cluster/foobar"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonKube)]
        public void NotEqualOperator()
        {
            Assert.False(KubeConfigName.Parse("user@cluster") != KubeConfigName.Parse("user@cluster"));
            Assert.False(KubeConfigName.Parse("user@cluster") != KubeConfigName.Parse("user@cluster/default"));
            Assert.False((KubeConfigName)null != (KubeConfigName)null);

            Assert.True(KubeConfigName.Parse("user@cluster/test") != KubeConfigName.Parse("foo@cluster/test"));
            Assert.True(KubeConfigName.Parse("user@cluster/test") != KubeConfigName.Parse("user@bar/test"));
            Assert.True(KubeConfigName.Parse("user@cluster/test") != KubeConfigName.Parse("user@cluster/foobar"));
            Assert.True(KubeConfigName.Parse("user@cluster/test") != (KubeConfigName)null);
            Assert.True((KubeConfigName)null != KubeConfigName.Parse("user@cluster/foobar"));
        }
    }
}

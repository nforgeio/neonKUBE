//-----------------------------------------------------------------------------
// FILE:	    Test_HiveLoadBalancer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Cryptography;
using Neon.Hive;
using Neon.Xunit;
using Neon.Xunit.Hive;

using Xunit;

namespace TestHive
{
    public partial class Test_HiveLoadBalancer : IClassFixture<HiveFixture>
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Uncached_DefaultPort()
        {
            await TestHttpRule("http-private-defaultport", HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Uncached_NonDefaultPort()
        {
            await TestHttpRule("http-private-nondefaultport", HiveHostPorts.ProxyPrivateLastUserPort, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Uncached_NoHostname()
        {
            await TestHttpRule("http-private-nohostname", HiveHostPorts.ProxyPrivateLastUserPort, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer, useIPAddress: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Cached_DefaultPort()
        {
            await TestHttpRule("http-private-cached-defaultport", HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Cached_NonDefaultPort()
        {
            await TestHttpRule("http-private-cached-nondefaultport", HiveHostPorts.ProxyPrivateLastUserPort, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Cached_NoHostname()
        {
            await TestHttpRule("http-private-cached-nohostname", HiveHostPorts.ProxyPrivateLastUserPort, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer, useCache: true, useIPAddress: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Uncached_MultiHostnames_DefaultPort()
        {
            await TestHttpMultipleFrontends("http-private-multihostnames-defaultport", new string[] { "vegomatic1.test", "vegomatic2.test" }, HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Uncached_MultiHostnames_NonDefaultPort()
        {
            await TestHttpMultipleFrontends("http-private-multihostnames-nondefaultport", new string[] { "vegomatic1.test", "vegomatic2.test" }, HiveHostPorts.ProxyPrivateLastUserPort, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Cached_MultiHostnames_DefaultPort()
        {
            await TestHttpMultipleFrontends("http-private-multihostnames-defaultport", new string[] { "vegomatic1.test", "vegomatic2.test" }, HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Cached_MultiHostnames_NondefaultPortd()
        {
            await TestHttpMultipleFrontends("http-private-multihostnames-nondefaultport", new string[] { "vegomatic1.test", "vegomatic2.test" }, HiveHostPorts.ProxyPrivateLastUserPort, HiveConst.PrivateNetwork, hive.PrivateLoadBalancer, useCache: true);
        }
    }
}

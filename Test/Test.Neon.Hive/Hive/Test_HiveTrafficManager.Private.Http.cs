//-----------------------------------------------------------------------------
// FILE:	    Test_HiveTrafficManager.Private.Http.cs
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
    public partial class Test_HiveTrafficManager : IClassFixture<HiveFixture>
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Uncached_DefaultPort()
        {
            await TestHttpRule("http-private-defaultport", HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Uncached_NonDefaultPort()
        {
            await TestHttpRule("http-private-nondefaultport", HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Uncached_NoHostname()
        {
            await TestHttpRule("http-private-nohostname", HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic, useIPAddress: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Cached_DefaultPort()
        {
            await TestHttpRule("http-private-cached-defaultport", HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Cached_NonDefaultPort()
        {
            await TestHttpRule("http-private-cached-nondefaultport", HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Cached_NoHostname()
        {
            await TestHttpRule("http-private-cached-nohostname", HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: true, useIPAddress: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Uncached_MultiFrontends_DefaultPort()
        {
            await TestHttpMultipleFrontends("http-private-multifrontends-defaultport", new string[] { $"test-1.{testHostname}", $"test-2.{testHostname}" }, HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Uncached_MultiFrontends_NonDefaultPort()
        {
            await TestHttpMultipleFrontends("http-private-multifrontends-nondefaultport", new string[] { $"test-1.{testHostname}", $"test-2.{testHostname}" }, HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Cached_MultiFrontends_DefaultPort()
        {
            await TestHttpMultipleFrontends("http-private-multihostnames-defaultport", new string[] { $"test-1.{testHostname}", $"test-2.{testHostname}" }, HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Cached_MultiFrontends_NonDefaultPort()
        {
            await TestHttpMultipleFrontends("http-private-multihostnames-nondefaultport", new string[] { $"test-1.{testHostname}", $"test-2.{testHostname}" }, HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Prefix_Uncached_DefaultPort()
        {
            await TestHttpPrefix("http-private-prefix-uncached-defaultport", HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: false);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Prefix_Uncached_NonDefaultPort()
        {
            await TestHttpPrefix("http-private-prefix-uncached-nondefaultport", HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: false);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Prefix_Cached_DefaultPort()
        {
            await TestHttpPrefix("http-private-prefix-cached-defaultport", HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Prefix_Cached_NonDefaultPort()
        {
            await TestHttpPrefix("http-private-prefix-cached-nondefaultport", HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Cached_Warm_DefaultPort()
        {
            await TestHttpCacheWarming("http-private-cache-warm-defaultport", HiveHostPorts.ProxyPrivateHttp, HiveConst.PrivateNetwork, hive.PrivateTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Private_Cached_Warm_NonDefaultPort()
        {
            await TestHttpCacheWarming("http-private-cache-warm-nondefaultport", HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic);
        }
    }
}

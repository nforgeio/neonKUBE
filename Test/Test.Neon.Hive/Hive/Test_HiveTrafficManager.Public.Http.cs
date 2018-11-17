//-----------------------------------------------------------------------------
// FILE:	    Test_HiveTrafficManager.Public.Http.cs
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
        public async Task Http_Public_Uncached_DefaultPort()
        {
            await TestHttpRule("http-public-defaultport", HiveHostPorts.ProxyPublicHttp, HiveConst.PublicNetwork, hive.PublicTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Uncached_NonDefaultPort()
        {
            await TestHttpRule("http-public-nondefaultport", HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Uncached_NoHostname()
        {
            await TestHttpRule("http-public-nohostname", HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic, useIPAddress: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Cached_DefaultPort()
        {
            await TestHttpRule("http-public-cached-defaultport", HiveHostPorts.ProxyPublicHttp, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Cached_NonDefaultPort()
        {
            await TestHttpRule("http-public-cached-nondefaultport", HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Cached_NoHostname()
        {
            await TestHttpRule("http-public-cached-nohostname", HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: true, useIPAddress: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Uncached_MultiFrontends_DefaultPort()
        {
            await TestHttpMultipleFrontends("http-public-multifrontends-defaultport", new string[] { $"test-1.{testHostname}", $"test-2.{testHostname}" }, HiveHostPorts.ProxyPublicHttp, HiveConst.PublicNetwork, hive.PublicTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Uncached_MultiFrontends_NonDefaultPort()
        {
            await TestHttpMultipleFrontends("http-public-multifrontends-nondefaultport", new string[] { $"test-1.{testHostname}", $"test-2.{testHostname}" }, HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Cached_MultiFrontends_DefaultPort()
        {
            await TestHttpMultipleFrontends("http-public-multifrontends-defaultport", new string[] { $"test-1.{testHostname}", $"test-2.{testHostname}" }, HiveHostPorts.ProxyPublicHttp, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Cached_MultiFrontends_NonDefaultPort()
        {
            await TestHttpMultipleFrontends("http-public-multifrontends-nondefaultport", new string[] { $"test-1.{testHostname}", $"test-2.{testHostname}" }, HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Prefix_Uncached_DefaultPort()
        {
            await TestHttpPrefix("http-public-prefix-uncached-defaultport", HiveHostPorts.ProxyPublicHttp, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: false);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Prefix_Uncached_NonDefaultPort()
        {
            await TestHttpPrefix("http-public-prefix-uncached-nondefaultport", HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: false);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Prefix_Cached_DefaultPort()
        {
            await TestHttpPrefix("http-public-prefix-cached-defaultport", HiveHostPorts.ProxyPublicHttp, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Prefix_Cached_NonDefaultPort()
        {
            await TestHttpPrefix("http-public-prefix-cached-nondefaultport", HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Cached_Warm_DefaultPort()
        {
            await TestHttpCacheWarming("http-public-cache-warm-defaultport", HiveHostPorts.ProxyPublicHttp, HiveConst.PublicNetwork, hive.PublicTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Http_Public_Cached_Warm_NonDefaultPort()
        {
            await TestHttpCacheWarming("http-public-cache-warm-nondefaultport", HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic);
        }
    }
}

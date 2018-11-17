//-----------------------------------------------------------------------------
// FILE:	    Test_HiveTrafficManager.Public.Https.cs
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
        public async Task Https_Public_Uncached_DefaultPort()
        {
            await TestHttpsRule("https-public-defaultport", HiveHostPorts.ProxyPublicHttps, HiveConst.PublicNetwork, hive.PublicTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Public_Uncached_NonDefaultPort()
        {
            await TestHttpsRule("https-public-nondefaultport", HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Public_Uncached_NoHostname()
        {
            await TestHttpsRule("https-public-nohostname", HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Public_Cached_DefaultPort()
        {
            await TestHttpsRule("https-public-cached-defaultport", HiveHostPorts.ProxyPublicHttps, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Public_Cached_NonDefaultPort()
        {
            await TestHttpsRule("https-public-cached-nondefaultport", HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Public_Cached_NoHostname()
        {
            await TestHttpsRule("https-public-cached-nohostname", HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Public_Uncached_MultiFrontends_DefaultPort()
        {
            await TestHttpsMultipleFrontends("https-public-multifrontends-defaultport", new string[] { $"test-1.{testHostname}", $"test-2.{testHostname}" }, HiveHostPorts.ProxyPublicHttps, HiveConst.PublicNetwork, hive.PublicTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Public_Uncached_MultiFrontends_NonDefaultPort()
        {
            await TestHttpsMultipleFrontends("https-public-multifrontend-nondefaultport", new string[] { $"test-1.{testHostname}", $"test-2.{testHostname}" }, HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Public_Cached_MultiFrontends_DefaultPort()
        {
            await TestHttpsMultipleFrontends("https-public-multifrontend-defaultport", new string[] { $"test-1.{testHostname}", $"test-2.{testHostname}" }, HiveHostPorts.ProxyPublicHttps, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Public_Cached_MultiFrontends_NonDefaultPortd()
        {
            await TestHttpsMultipleFrontends("https-public-multifrontend-nondefaultport", new string[] { $"test-1.{testHostname}", $"test-2.{testHostname}" }, HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Public_Prefix_Uncached_DefaultPort()
        {
            await TestHttpsPrefix("https-public-prefix-uncached-defaultport", HiveHostPorts.ProxyPublicHttps, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: false);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Public_Prefix_Uncached_NonDefaultPort()
        {
            await TestHttpsPrefix("https-public-prefix-uncached-nondefaultport", HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: false);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Public_Prefix_Cached_DefaultPort()
        {
            await TestHttpsPrefix("https-public-prefix-cached-defaultport", HiveHostPorts.ProxyPublicHttps, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Public_Prefix_Cached_NonDefaultPort()
        {
            await TestHttpsPrefix("https-public-prefix-cached-nondefaultport", HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Public_Cached_Warm_DefaultPort()
        {
            await TestHttpsCacheWarming("https-public-cache-warm-defaultport", HiveHostPorts.ProxyPublicHttps, HiveConst.PublicNetwork, hive.PublicTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Public_Cached_Warm_NonDefaultPort()
        {
            await TestHttpsCacheWarming("https-public-cache-warm-nondefaultport", HiveHostPorts.ProxyPublicLastUser, HiveConst.PublicNetwork, hive.PublicTraffic);
        }
    }
}

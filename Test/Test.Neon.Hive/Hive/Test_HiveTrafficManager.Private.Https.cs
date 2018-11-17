//-----------------------------------------------------------------------------
// FILE:	    Test_HiveTrafficManager.Private.Https.cs
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
        public async Task Https_Private_Uncached_DefaultPort()
        {
            await TestHttpsRule("https-private-defaultport", HiveHostPorts.ProxyPrivateHttps, HiveConst.PrivateNetwork, hive.PrivateTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Private_Uncached_NonDefaultPort()
        {
            await TestHttpsRule("https-private-nondefaultport", HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Private_Uncached_NoHostname()
        {
            await TestHttpsRule("https-private-nohostname", HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Private_Cached_DefaultPort()
        {
            await TestHttpsRule("https-private-cached-defaultport", HiveHostPorts.ProxyPrivateHttps, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Private_Cached_NonDefaultPort()
        {
            await TestHttpsRule("https-private-cached-nondefaultport", HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Private_Cached_NoHostname()
        {
            await TestHttpsRule("https-private-cached-nohostname", HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Private_Uncached_MultiFrontends_DefaultPort()
        {
            await TestHttpsMultipleFrontends("https-private-multifrontends-defaultport", new string[] { $"test-1.{testHostname}", $"test-2.{testHostname}" }, HiveHostPorts.ProxyPrivateHttps, HiveConst.PrivateNetwork, hive.PrivateTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Private_Uncached_MultiFrontends_NonDefaultPort()
        {
            await TestHttpsMultipleFrontends("https-private-multifrontend-nondefaultport", new string[] { $"test-1.{testHostname}", $"test-2.{testHostname}" }, HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Private_Cached_MultiFrontends_DefaultPort()
        {
            await TestHttpsMultipleFrontends("https-private-multifrontend-defaultport", new string[] { $"test-1.{testHostname}", $"test-2.{testHostname}" }, HiveHostPorts.ProxyPrivateHttps, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Private_Cached_MultiFrontends_NonDefaultPortd()
        {
            await TestHttpsMultipleFrontends("https-private-multifrontend-nondefaultport", new string[] { $"test-1.{testHostname}", $"test-2.{testHostname}" }, HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Private_Prefix_Uncached_DefaultPort()
        {
            await TestHttpsPrefix("https-private-prefix-uncached-defaultport", HiveHostPorts.ProxyPrivateHttps, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: false);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Private_Prefix_Uncached_NonDefaultPort()
        {
            await TestHttpPrefix("https-private-prefix-uncached-nondefaultport", HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: false);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Private_Prefix_Cached_DefaultPort()
        {
            await TestHttpsPrefix("https-private-prefix-cached-defaultport", HiveHostPorts.ProxyPrivateHttps, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Private_Prefix_Cached_NonDefaultPort()
        {
            await TestHttpsPrefix("https-private-prefix-cached-nondefaultport", HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic, useCache: true);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Private_Cached_Warm_DefaultPort()
        {
            await TestHttpsCacheWarming("https-private-cache-warm-defaultport", HiveHostPorts.ProxyPrivateHttps, HiveConst.PrivateNetwork, hive.PrivateTraffic);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public async Task Https_Private_Cached_Warm_NonDefaultPort()
        {
            await TestHttpsCacheWarming("https-private-cache-warm-nondefaultport", HiveHostPorts.ProxyPrivateLastUser, HiveConst.PrivateNetwork, hive.PrivateTraffic);
        }
    }
}

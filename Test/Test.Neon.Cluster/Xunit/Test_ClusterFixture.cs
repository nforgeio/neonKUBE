//-----------------------------------------------------------------------------
// FILE:	    Test_ClusterFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Consul;

using Neon.Common;
using Neon.Cluster;
using Neon.Cryptography;

using Xunit;
using Xunit.Neon;

namespace TestNeonCluster
{
    public class Test_ClusterFixture : IClassFixture<ClusterFixture>
    {
        private ClusterFixture cluster;

        public Test_ClusterFixture(ClusterFixture cluster)
        {
            this.cluster = cluster;

            // We're passing [login=null] below to connect to the cluster specified
            // by the NEON_TEST_CLUSTER environment variable.  This needs to be 
            // initialized with the login for a deployed cluster.

            if (this.cluster.LoginAndInitialize())
            {
                cluster.Reset();

                NeonHelper.WaitParallel(
                    new Action[]
                    {
                        () => cluster.CreateSecret("secret_text", "hello"),
                        () => cluster.CreateSecret("secret_data", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }),
                        () => cluster.CreateConfig("config_text", "hello"),
                        () => cluster.CreateConfig("config_data", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }),
                        () => cluster.CreateNetwork("test-network"),
                        () => cluster.CreateService("test-service", "neoncluster/test"),
                        () =>
                        {
                            var composeText =
@"version: '3'

services:
  sleeper:
    image: neoncluster/test
    deploy:
      replicas: 2
";
                            cluster.DeployStack("test-stack", composeText);
                        },
                        () =>
                        {
                            var publicRule = new LoadBalancerTcpRule();

                            publicRule.Name = "test-rule";
                            publicRule.Frontends.Add(new LoadBalancerTcpFrontend() { ProxyPort = NeonHostPorts.ProxyPublicFirstUserPort });
                            publicRule.Backends.Add(new LoadBalancerTcpBackend() { Server = "127.0.0.1", Port = 10000 });

                            cluster.PutLoadBalancerRule("public", publicRule);
                        },
                        () =>
                        {
                            var privateRule = new LoadBalancerTcpRule();

                            privateRule.Name = "test-rule";
                            privateRule.Frontends.Add(new LoadBalancerTcpFrontend() { ProxyPort = NeonHostPorts.ProxyPrivateFirstUserPort });
                            privateRule.Backends.Add(new LoadBalancerTcpBackend() { Server = "127.0.0.1", Port = 10000 });

                            cluster.PutLoadBalancerRule("private", privateRule);
                            cluster.PutCertificate("test-certificate", TestCertificate.CombinedPem);
                        },
                        async () => await cluster.Consul.KV.PutString("test/value1", "one"),
                        async () => await cluster.Consul.KV.PutString("test/value2", "two"),
                        async () => await cluster.Consul.KV.PutString("test/folder/value3", "three"),
                        async () => await cluster.Consul.KV.PutString("test/folder/value4", "four")
                    });
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCluster)]
        public void VerifyState()
        {
            // Verify that the various cluster objects were created by the constructor.

            Assert.Single(cluster.ListSecrets().Where(item => item.Name == "secret_text"));
            Assert.Single(cluster.ListSecrets().Where(item => item.Name == "secret_data"));

            Assert.Single(cluster.ListConfigs().Where(item => item.Name == "config_text"));
            Assert.Single(cluster.ListConfigs().Where(item => item.Name == "config_data"));

            Assert.Single(cluster.ListNetworks().Where(item => item.Name == "test-network"));

            Assert.Single(cluster.ListServices().Where(item => item.Name == "test-service"));

            var stack = cluster.ListStacks().SingleOrDefault(item => item.Name == "test-stack");

            Assert.NotNull(stack);
            Assert.Equal(1, stack.ServiceCount);
            Assert.Single(cluster.ListServices().Where(item => item.Name.Equals("test-stack_sleeper")));

            Assert.Single(cluster.ListLoagBalancerRules("public"));
            Assert.Single(cluster.ListLoagBalancerRules("public").Where(item => item.Name == "test-rule"));

            Assert.Single(cluster.ListLoagBalancerRules("private"));
            Assert.Single(cluster.ListLoagBalancerRules("private").Where(item => item.Name == "test-rule"));

            Assert.Single(cluster.ListCertificates());
            Assert.Single(cluster.ListCertificates().Where(item => item == "test-certificate"));

            Assert.Equal("one", cluster.Consul.KV.GetString("test/value1").Result);
            Assert.Equal("two", cluster.Consul.KV.GetString("test/value2").Result);
            Assert.Equal("three", cluster.Consul.KV.GetString("test/folder/value3").Result);
            Assert.Equal("four", cluster.Consul.KV.GetString("test/folder/value4").Result);

            // Now reset the cluster and verify that all state was cleared.

            cluster.Reset();

            Assert.Empty(cluster.ListServices());
            Assert.Empty(cluster.ListStacks());
            Assert.Empty(cluster.ListSecrets());
            Assert.Empty(cluster.ListConfigs());
            Assert.Empty(cluster.ListNetworks());
            Assert.Empty(cluster.ListLoagBalancerRules("public"));
            Assert.Empty(cluster.ListLoagBalancerRules("private"));
            Assert.Empty(cluster.ListCertificates());

            Assert.False(cluster.Consul.KV.Exists("test/value1").Result);
            Assert.False(cluster.Consul.KV.Exists("test/value2").Result);
            Assert.False(cluster.Consul.KV.Exists("test/folder/value3").Result);
            Assert.False(cluster.Consul.KV.Exists("test/folder/value4").Result);
        }

        /// <summary>
        /// Returns a self-signed PEM encoded certificate and private key for testing purposes.
        /// </summary>
        private TlsCertificate TestCertificate
        {
            get { return TlsCertificate.CreateSelfSigned("test.com"); }
        }
    }
}

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
using Neon.Cryptography;
using Neon.Hive;
using Neon.Xunit;
using Neon.Xunit.Hive;

using Xunit;

namespace TestNeonCluster
{
    public class Test_ClusterFixture : IClassFixture<HiveFixture>
    {
        private HiveFixture     hive;
        private ClusterProxy    cluster;

        public Test_ClusterFixture(HiveFixture cluster)
        {
            // We're passing [login=null] below to connect to the cluster specified
            // by the NEON_TEST_CLUSTER environment variable.  This needs to be 
            // initialized with the login for a deployed cluster.

            if (cluster.LoginAndInitialize())
            {
                cluster.Reset();

                NeonHelper.WaitForParallel(
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
                            publicRule.Frontends.Add(new LoadBalancerTcpFrontend() { ProxyPort = HiveHostPorts.ProxyPublicFirstUserPort });
                            publicRule.Backends.Add(new LoadBalancerTcpBackend() { Server = "127.0.0.1", Port = 10000 });

                            cluster.PutLoadBalancerRule("public", publicRule);
                        },
                        () =>
                        {
                            var privateRule = new LoadBalancerTcpRule();

                            privateRule.Name = "test-rule";
                            privateRule.Frontends.Add(new LoadBalancerTcpFrontend() { ProxyPort = HiveHostPorts.ProxyPrivateFirstUserPort });
                            privateRule.Backends.Add(new LoadBalancerTcpBackend() { Server = "127.0.0.1", Port = 10000 });

                            cluster.PutLoadBalancerRule("private", privateRule);
                            cluster.PutCertificate("test-certificate", TestCertificate.CombinedPem);
                            cluster.SetSelfSignedCertificate("test-certificate2", "*.foo.com");
                        },
                        async () => await cluster.Consul.KV.PutString("test/value1", "one"),
                        async () => await cluster.Consul.KV.PutString("test/value2", "two"),
                        async () => await cluster.Consul.KV.PutString("test/folder/value3", "three"),
                        async () => await cluster.Consul.KV.PutString("test/folder/value4", "four")
                    });
            }

            this.hive = cluster;
            this.cluster = cluster.Cluster;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCluster)]
        public void VerifyState()
        {
            // Verify that the various cluster objects were created by the constructor.

            Assert.Single(hive.ListSecrets().Where(item => item.Name == "secret_text"));
            Assert.Single(hive.ListSecrets().Where(item => item.Name == "secret_data"));

            Assert.Single(hive.ListConfigs().Where(item => item.Name == "config_text"));
            Assert.Single(hive.ListConfigs().Where(item => item.Name == "config_data"));

            Assert.Single(hive.ListNetworks().Where(item => item.Name == "test-network"));

            Assert.Single(hive.ListServices().Where(item => item.Name == "test-service"));

            var stack = hive.ListStacks().SingleOrDefault(item => item.Name == "test-stack");

            Assert.NotNull(stack);
            Assert.Equal(1, stack.ServiceCount);
            Assert.Single(hive.ListServices().Where(item => item.Name.Equals("test-stack_sleeper")));

            Assert.Single(hive.ListLoadBalancerRules("public"));
            Assert.Single(hive.ListLoadBalancerRules("public").Where(item => item.Name == "test-rule"));

            Assert.Single(hive.ListLoadBalancerRules("private"));
            Assert.Single(hive.ListLoadBalancerRules("private").Where(item => item.Name == "test-rule"));

            Assert.Equal(2, hive.ListCertificates().Count);
            Assert.Single(hive.ListCertificates().Where(item => item == "test-certificate"));
            Assert.Single(hive.ListCertificates().Where(item => item == "test-certificate2"));

            Assert.Equal("one", hive.Consul.KV.GetString("test/value1").Result);
            Assert.Equal("two", hive.Consul.KV.GetString("test/value2").Result);
            Assert.Equal("three", hive.Consul.KV.GetString("test/folder/value3").Result);
            Assert.Equal("four", hive.Consul.KV.GetString("test/folder/value4").Result);

            // Now reset the cluster and verify that all state was cleared.

            hive.Reset();

            Assert.Empty(hive.ListServices());
            Assert.Empty(hive.ListStacks());
            Assert.Empty(hive.ListSecrets());
            Assert.Empty(hive.ListConfigs());
            Assert.Empty(hive.ListNetworks());
            Assert.Empty(hive.ListLoadBalancerRules("public"));
            Assert.Empty(hive.ListLoadBalancerRules("private"));
            Assert.Empty(hive.ListCertificates());

            Assert.False(hive.Consul.KV.Exists("test/value1").Result);
            Assert.False(hive.Consul.KV.Exists("test/value2").Result);
            Assert.False(hive.Consul.KV.Exists("test/folder/value3").Result);
            Assert.False(hive.Consul.KV.Exists("test/folder/value4").Result);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCluster)]
        public void ClearVolumes()
        {
            //-----------------------------------------------------------------
            // Create a test volume on each of the cluster nodes and then verify
            // that ClearVolumes() removes them.

            var actions = new List<Action>();

            foreach (var node in cluster.Nodes)
            {
                actions.Add(
                    () =>
                    {
                        using (var nodeClone = node.Clone())
                        {
                            nodeClone.SudoCommand("docker volume create test-volume", RunOptions.None);
                        }
                    });
            }

            NeonHelper.WaitForParallel(actions);

            hive.ClearVolumes();

            var sbUncleared = new StringBuilder();

            actions.Clear();

            foreach (var node in cluster.Nodes)
            {
                actions.Add(
                    () =>
                    {
                        using (var nodeClone = node.Clone())
                        {
                            var response = nodeClone.SudoCommand("docker volume ls --format \"{{.Name}}\"", RunOptions.None);

                            if (response.ExitCode != 0)
                            {
                                lock (sbUncleared)
                                {
                                    sbUncleared.AppendLine($"{nodeClone.Name}: exitcode={response.ExitCode} message={response.AllText}");
                                }
                            }
                            else if (response.AllText.Contains("test-volume"))
                            {
                                lock (sbUncleared)
                                {
                                    sbUncleared.AppendLine($"{nodeClone.Name}: [test-volume] still exists.");
                                }
                            }
                        }
                    });
            }

            NeonHelper.WaitForParallel(actions);
            Assert.Empty(sbUncleared.ToString());
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

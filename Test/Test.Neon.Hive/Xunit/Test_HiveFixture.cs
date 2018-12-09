//-----------------------------------------------------------------------------
// FILE:	    Test_HiveFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Consul;
using EasyNetQ.Management.Client.Model;

using Neon.Common;
using Neon.Cryptography;
using Neon.Hive;
using Neon.Xunit;
using Neon.Xunit.Hive;

using Xunit;

namespace TestHive
{
    public class Test_HiveFixture : IClassFixture<HiveFixture>
    {
        private HiveFixture hiveFixture;
        private HiveProxy hive;

        public Test_HiveFixture(HiveFixture hive)
        {
            // We're passing [login=null] below to connect to the hive specified
            // by the NEON_TEST_HIVE environment variable.  This needs to be 
            // initialized with the login for a deployed hive.

            if (hive.LoginAndInitialize())
            {
                hive.Reset();
            }

            this.hiveFixture = hive;
            this.hive = hive.Hive;

            // Initialize the hive state.

            NeonHelper.WaitForParallel(
                new Action[]
                {
                    () => hive.CreateSecret("secret_text", "hello"),
                    () => hive.CreateSecret("secret_data", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }),
                    () => hive.CreateConfig("config_text", "hello"),
                    () => hive.CreateConfig("config_data", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 }),
                    () => hive.CreateNetwork("test-network"),
                    () => hive.CreateService("test-service", "nhive/test"),
                    async () =>
                    {
                        // Create the HiveMQ [test] user, [test] vhost along with the [test-queue].

                        using (var mqManager = hive.ConnectHiveMQManager())
                        {
                            var mqUser  = await mqManager.CreateUserAsync(new UserInfo("test-user", "password"));
                            var mqVHost = await mqManager.CreateVirtualHostAsync("test-vhost");
                            var mqQueue = await mqManager.CreateQueueAsync(new QueueInfo("test-queue", autoDelete: false, durable: true, arguments: new InputArguments()), mqVHost);

                            await mqManager.CreatePermissionAsync(new PermissionInfo(mqUser, mqVHost));
                        }
                    },
                    () =>
                    {
                        var composeText =
@"version: '3'

services:
  sleeper:
    image: nhive/test
    deploy:
      replicas: 2
";
                        hive.DeployStack("test-stack", composeText);
                    },
                    () =>
                    {
                        var publicRule = new TrafficTcpRule();

                        publicRule.Name = "test-rule";
                        publicRule.Frontends.Add(new TrafficTcpFrontend() { ProxyPort = HiveHostPorts.ProxyPublicFirstUser });
                        publicRule.Backends.Add(new TrafficTcpBackend() { Server = "127.0.0.1", Port = 10000 });

                        hive.PutTrafficManagerRule("public", publicRule);
                    },
                    () =>
                    {
                        var privateRule = new TrafficTcpRule();

                        privateRule.Name = "test-rule";
                        privateRule.Frontends.Add(new TrafficTcpFrontend() { ProxyPort = HiveHostPorts.ProxyPrivateFirstUser });
                        privateRule.Backends.Add(new TrafficTcpBackend() { Server = "127.0.0.1", Port = 10000 });

                        hive.PutTrafficManagerRule("private", privateRule);
                        hive.PutCertificate("test-certificate", TestCertificate.CombinedPem);
                        hive.SetSelfSignedCertificate("test-certificate2", "*.foo.com");
                    },
                    async () => await hive.Consul.KV.PutString("test/value1", "one"),
                    async () => await hive.Consul.KV.PutString("test/value2", "two"),
                    async () => await hive.Consul.KV.PutString("test/folder/value3", "three"),
                    async () => await hive.Consul.KV.PutString("test/folder/value4", "four")
                });
        }

        /// <summary>
        /// Returns a self-signed PEM encoded certificate and private key for testing purposes.
        /// </summary>
        private TlsCertificate TestCertificate
        {
            get { return TlsCertificate.CreateSelfSigned("test.com"); }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public void VerifyState()
        {
            // Verify that the various hive objects were initialized by the constructor.

            Assert.Single(hiveFixture.ListSecrets().Where(item => item.Name == "secret_text"));
            Assert.Single(hiveFixture.ListSecrets().Where(item => item.Name == "secret_data"));

            Assert.Single(hiveFixture.ListConfigs().Where(item => item.Name == "config_text"));
            Assert.Single(hiveFixture.ListConfigs().Where(item => item.Name == "config_data"));

            Assert.Single(hiveFixture.ListNetworks().Where(item => item.Name == "test-network"));

            Assert.Single(hiveFixture.ListServices().Where(item => item.Name == "test-service"));

            var stack = hiveFixture.ListStacks().SingleOrDefault(item => item.Name == "test-stack");

            Assert.NotNull(stack);
            Assert.Equal(1, stack.ServiceCount);
            Assert.Single(hiveFixture.ListServices().Where(item => item.Name.Equals("test-stack_sleeper")));

            Assert.Single(hiveFixture.ListTrafficManagers("public"));
            Assert.Single(hiveFixture.ListTrafficManagers("public").Where(item => item.Name == "test-rule"));

            Assert.Single(hiveFixture.ListTrafficManagers("private"));
            Assert.Single(hiveFixture.ListTrafficManagers("private").Where(item => item.Name == "test-rule"));

            Assert.Equal(2, hiveFixture.ListCertificates().Count);
            Assert.Single(hiveFixture.ListCertificates().Where(item => item == "test-certificate"));
            Assert.Single(hiveFixture.ListCertificates().Where(item => item == "test-certificate2"));

            Assert.Equal("one", hiveFixture.Consul.KV.GetString("test/value1").Result);
            Assert.Equal("two", hiveFixture.Consul.KV.GetString("test/value2").Result);
            Assert.Equal("three", hiveFixture.Consul.KV.GetString("test/folder/value3").Result);
            Assert.Equal("four", hiveFixture.Consul.KV.GetString("test/folder/value4").Result);

            using (var mqManager = hive.HiveMQ.ConnectHiveMQManager())
            {
                Assert.Single(mqManager.GetUsersAsync().Result.Where(u => u.Name == "test-user"));
                Assert.Single(mqManager.GetVHostsAsync().Result.Where(vh => vh.Name == "test-vhost"));
                Assert.Single(mqManager.GetQueuesAsync().Result.Where(q => q.Name == "test-queue"));
            }

            // Now reset the hive and verify that all state was cleared.

            hiveFixture.Reset();

            Assert.Empty(hiveFixture.ListServices());
            Assert.Empty(hiveFixture.ListStacks());
            Assert.Empty(hiveFixture.ListSecrets());
            Assert.Empty(hiveFixture.ListConfigs());
            Assert.Empty(hiveFixture.ListNetworks());
            Assert.Empty(hiveFixture.ListTrafficManagers("public"));
            Assert.Empty(hiveFixture.ListTrafficManagers("private"));
            Assert.Empty(hiveFixture.ListCertificates());

            Assert.False(hiveFixture.Consul.KV.Exists("test/value1").Result);
            Assert.False(hiveFixture.Consul.KV.Exists("test/value2").Result);
            Assert.False(hiveFixture.Consul.KV.Exists("test/folder/value3").Result);
            Assert.False(hiveFixture.Consul.KV.Exists("test/folder/value4").Result);

            using (var mqManager = hive.HiveMQ.ConnectHiveMQManager())
            {
                Assert.Empty(mqManager.GetUsersAsync().Result.Where(u => u.Name == "test-user"));
                Assert.Empty(mqManager.GetVHostsAsync().Result.Where(vh => vh.Name == "test-vhost"));
                Assert.Empty(mqManager.GetQueuesAsync().Result.Where(q => q.Name == "test-queue"));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public void ClearVolumes()
        {
            //-----------------------------------------------------------------
            // Create a test volume on each of the hive nodes and then verify
            // that ClearVolumes() removes them.

            var actions = new List<Action>();

            foreach (var node in hive.Nodes)
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

            hiveFixture.ClearVolumes();

            var sbUncleared = new StringBuilder();

            actions.Clear();

            foreach (var node in hive.Nodes)
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
    }
}

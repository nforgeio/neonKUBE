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

            if (!this.cluster.LoginAndInitialize())
            {
                cluster.Reset();
            }

            cluster.CreateSecret("secret_text", "hello");
            cluster.CreateSecret("secret_data", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

            cluster.CreateConfig("config_text", "hello");
            cluster.CreateConfig("config_data", new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 });

            cluster.CreateNetwork("test-network");

            cluster.CreateService("test-service", "alpine", serviceArgs: new string[] { "sleep", "1000000" });

            var composeText =
@"version: '3'

services:
  sleeper:
    image: alpine
    command: sleep 1000000
    deploy:
      replicas: 2
";
            cluster.DeployStack("test-stack", composeText);

            var publicRoute = new ProxyTcpRoute();

            publicRoute.Name = "test-route";
            publicRoute.Frontends.Add(new ProxyTcpFrontend() { ProxyPort = NeonHostPorts.ProxyPublicFirstUserPort });
            publicRoute.Backends.Add(new ProxyTcpBackend() { Server = "127.0.0.1", Port = 10000 });

            cluster.PutProxyRoute("public", publicRoute);

            var privateRoute = new ProxyTcpRoute();

            privateRoute.Name = "test-route";
            privateRoute.Frontends.Add(new ProxyTcpFrontend() { ProxyPort = NeonHostPorts.ProxyPrivateFirstUserPort });
            privateRoute.Backends.Add(new ProxyTcpBackend() { Server = "127.0.0.1", Port = 10000 });

            cluster.PutProxyRoute("private", privateRoute);
            cluster.PutCertificate("test-certificate", TestCertificate.CombinedPem);
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

            Assert.Single(cluster.ListProxyRoutes("public"));
            Assert.Single(cluster.ListProxyRoutes("public").Where(item => item.Name == "test-route"));

            Assert.Single(cluster.ListProxyRoutes("private"));
            Assert.Single(cluster.ListProxyRoutes("private").Where(item => item.Name == "test-route"));

            Assert.Single(cluster.ListCertificates());
            Assert.Single(cluster.ListCertificates().Where(item => item == "test-certificate"));

            // Now reset the cluster and verify that all state was cleared.

            cluster.Reset();

            Assert.Empty(cluster.ListServices());
            Assert.Empty(cluster.ListStacks());
            Assert.Empty(cluster.ListSecrets());
            Assert.Empty(cluster.ListConfigs());
            Assert.Empty(cluster.ListNetworks());
            Assert.Empty(cluster.ListProxyRoutes("public"));
            Assert.Empty(cluster.ListProxyRoutes("private"));
            Assert.Empty(cluster.ListCertificates());
        }

        /// <summary>
        /// Returns a self-signed PEM encoded certificate and private key for testing purposes.
        /// </summary>
        private TlsCertificate TestCertificate
        {
            get { return TlsCertificate.CreateSelfSigned("test.com", 2048); }
        }
    }
}

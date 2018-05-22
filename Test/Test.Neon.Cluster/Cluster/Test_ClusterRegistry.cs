//-----------------------------------------------------------------------------
// FILE:	    Test_ClusterRegistry.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Cluster;
using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.Cluster;

using Xunit;

namespace TestNeonCluster
{
    public class Test_ClusterRegistry : IClassFixture<ClusterFixture>
    {
        private ClusterFixture  cluster;
        private ClusterProxy    clusterProxy;

        public Test_ClusterRegistry(ClusterFixture cluster)
        {
            if (!cluster.LoginAndInitialize())
            {
                cluster.Reset();
            }

            this.cluster      = cluster;
            this.clusterProxy = cluster.Cluster;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void RegistryCredentials()
        {
            // Verify that we can set, update, list, and remove Docker
            // registry credentials persisted to the cluster Vault.

            //-----------------------------------------------------------------
            // Save two registry credentials and then read to verify.

            clusterProxy.SetRegistryCredential("registry1.test.com", "billy", "bob");
            clusterProxy.SetRegistryCredential("registry2.test.com", "sally", "sue");

            var credential = clusterProxy.GetRegistryCredential("registry1.test.com");

            Assert.Equal("registry1.test.com", credential.Registry);
            Assert.Equal("billy", credential.Username);
            Assert.Equal("bob", credential.Password);

            credential = clusterProxy.GetRegistryCredential("registry2.test.com");

            Assert.Equal("registry2.test.com", credential.Registry);
            Assert.Equal("sally", credential.Username);
            Assert.Equal("sue", credential.Password);

            //-----------------------------------------------------------------
            // Verify that NULL is returned for a registry that doesn't exist.

            Assert.Null(clusterProxy.GetRegistryCredential("BAD.REGISTRY"));

            //-----------------------------------------------------------------
            // Verify that we can list credentials.

            var billyOK = false;
            var sallyOK = false;

            foreach (var item in clusterProxy.ListRegistryCredentials())
            {
                switch (item.Registry)
                {
                    case "registry1.test.com":

                        Assert.Equal("billy", item.Username);
                        Assert.Equal("bob", item.Password);
                        billyOK = true;
                        break;

                    case "registry2.test.com":

                        Assert.Equal("sally", item.Username);
                        Assert.Equal("sue", item.Password);
                        sallyOK = true;
                        break;
                }
            }

            Assert.True(billyOK);
            Assert.True(sallyOK);

            //-----------------------------------------------------------------
            // Verify that we can update a credential.

            clusterProxy.SetRegistryCredential("registry2.test.com", "sally", "sue-bob");

            credential = clusterProxy.GetRegistryCredential("registry2.test.com");

            Assert.Equal("registry2.test.com", credential.Registry);
            Assert.Equal("sally", credential.Username);
            Assert.Equal("sue-bob", credential.Password);

            //-----------------------------------------------------------------
            // Verify that we can remove credentials, without impacting the
            // remaining credentials.

            clusterProxy.RemoveRegistryCredential("registry1.test.com");
            Assert.Null(clusterProxy.GetRegistryCredential("registry1.test.com"));

            credential = clusterProxy.GetRegistryCredential("registry2.test.com");

            Assert.Equal("registry2.test.com", credential.Registry);
            Assert.Equal("sally", credential.Username);
            Assert.Equal("sue-bob", credential.Password);

            clusterProxy.RemoveRegistryCredential("registry2.test.com");
            Assert.Null(clusterProxy.GetRegistryCredential("registry2.test.com"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void LocalRegistry()
        {
            // Verify that we can use the [neon-registry] image to deploy
            // a local registry to the cluster.


        }
    }
}

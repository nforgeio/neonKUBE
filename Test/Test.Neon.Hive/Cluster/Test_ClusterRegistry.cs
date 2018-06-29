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

using Neon.Common;
using Neon.Hive;
using Neon.Xunit;
using Neon.Xunit.Hive;

using Xunit;

namespace TestNeonCluster
{
    public class Test_ClusterRegistry : IClassFixture<ClusterFixture>
    {
        private ClusterFixture  fixture;
        private ClusterProxy    cluster;

        public Test_ClusterRegistry(ClusterFixture fixture)
        {
            if (!fixture.LoginAndInitialize())
            {
                fixture.Reset();
            }

            this.fixture = fixture;
            this.cluster = fixture.Cluster;
        }

        [Fact(Skip = "todo")]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void RegistryCredentials()
        {
            // $todo(jeff.lill):
            //
            // This test needs some work.  It depends on the existence of two
            // Docker registries which don't exist at this point.  The simple
            // solution would be to deploy these locally as simple services
            // using the [neon-registry] image, install a certificate, setup
            // a cluster DNS host, and add a load balancer rule.

            // Verify that we can set, update, list, and remove Docker
            // registry credentials persisted to the cluster Vault.

            //-----------------------------------------------------------------
            // Save two registry credentials and then read to verify.

            cluster.Registry.Login("registry1.neonforge.net", "billy", "bob");
            cluster.Registry.Login("registry2.neonforge.net", "sally", "sue");

            var credential = cluster.Registry.GetCredentials("registry1.neonforge.net");

            Assert.Equal("registry1.neonforge.net", credential.Registry);
            Assert.Equal("billy", credential.Username);
            Assert.Equal("bob", credential.Password);

            credential = cluster.Registry.GetCredentials("registry2.neonforge.net");

            Assert.Equal("registry2.neonforge.net", credential.Registry);
            Assert.Equal("sally", credential.Username);
            Assert.Equal("sue", credential.Password);

            //-----------------------------------------------------------------
            // Verify that NULL is returned for a registry that doesn't exist.

            Assert.Null(cluster.Registry.GetCredentials("BAD.REGISTRY"));

            //-----------------------------------------------------------------
            // Verify that we can list credentials.

            var billyOK = false;
            var sallyOK = false;

            foreach (var item in cluster.Registry.List())
            {
                switch (item.Registry)
                {
                    case "registry1.neonforge.net":

                        Assert.Equal("billy", item.Username);
                        Assert.Equal("bob", item.Password);
                        billyOK = true;
                        break;

                    case "registry2.neonforge.net":

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

            cluster.Registry.Login("registry2.neonforge.net", "sally", "sue-bob");

            credential = cluster.Registry.GetCredentials("registry2.neonforge.net");

            Assert.Equal("registry2.neonforge.net", credential.Registry);
            Assert.Equal("sally", credential.Username);
            Assert.Equal("sue-bob", credential.Password);

            //-----------------------------------------------------------------
            // Verify that we can remove credentials, without impacting the
            // remaining credentials.

            cluster.Registry.Logout("registry1.neonforge.net");
            Assert.Null(cluster.Registry.GetCredentials("registry1.neonforge.net"));

            credential = cluster.Registry.GetCredentials("registry2.neonforge.net");

            Assert.Equal("registry2.neonforge.net", credential.Registry);
            Assert.Equal("sally", credential.Username);
            Assert.Equal("sue-bob", credential.Password);

            cluster.Registry.Logout("registry2.neonforge.net");
            Assert.Null(cluster.Registry.GetCredentials("registry2.neonforge.net"));
        }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    Test_HiveState.cs
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
using Neon.Hive;
using Neon.Xunit;
using Neon.Xunit.Hive;

using Xunit;

namespace TestHive
{
    public class Test_HiveState : IClassFixture<HiveFixture>
    {
        private HiveFixture     hiveFixture;
        private HiveProxy       hive;

        public Test_HiveState(HiveFixture fixture)
        {
            if (!fixture.LoginAndInitialize())
            {
                fixture.Reset();
            }

            this.hiveFixture = fixture;
            this.hive        = fixture.Hive;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public void SecretGet()
        {
            // Verify that we can set and retrieve a Docker secret.

            Assert.Empty(hiveFixture.ListSecrets().Where(s => s.Name == "test-secret1"));
            hive.Docker.Secret.Set("test-secret1", "don't tell anyone!");
            Assert.Equal("don't tell anyone!", hive.Docker.Secret.GetString("test-secret1"));

            Assert.Empty(hiveFixture.ListSecrets().Where(s => s.Name == "test-secret2"));
            hive.Docker.Secret.Set("test-secret2", new byte[] { 0, 1, 2, 3, 4 });
            Assert.Equal(new byte[] { 0, 1, 2, 3, 4 }, hive.Docker.Secret.GetBytes("test-secret2"));

            // Verify that any [neon-secret-retriever] services were removed.

            var services = hiveFixture.ListServices(includeSystem: true);

            Assert.Empty(services.Where(s => s.Name.StartsWith("neon-secret-retriever")));

            // Verify that any temporary secret Consul keys were also removed.

            Assert.Empty(hive.Consul.Client.KV.ListKeys("neon/service/neon-secret-retriever").Result);
        }
    }
}

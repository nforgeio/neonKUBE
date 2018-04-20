//-----------------------------------------------------------------------------
// FILE:	    Test_CbHelper.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Couchbase;

using Neon.Common;

using Xunit;

namespace TestCouchbase
{
    public class Test_NeonBucket : IClassFixture<CouchbaseFixture>
    {
        private const string username = "Administrator";
        private const string password = "password";

        private CouchbaseFixture    fixture;
        private NeonBucket          bucket;

        public Test_NeonBucket(CouchbaseFixture fixture)
        {
            this.fixture = fixture;

            fixture.Initialize(
                () =>
                {
                   fixture.Start();
                });

            bucket = fixture.Bucket;
        }

        [Fact]
        public async Task BasicAsync()
        {
            // Basic test to verify that we can put/get/remove a document.

            await bucket.UpsertSafeAsync("hello", "world!");
            Assert.Equal("world!", await bucket.GetSafeAsync<string>("hello"));
            await bucket.RemoveSafeAsync("hello");
            Assert.Null(await bucket.FindSafeAsync<string>("hello"));
        }

        [Fact]
        public async Task CasTransientDetector()
        {
            // Verify that the CAS transient detector function works.

            var result = await bucket.UpsertAsync("test", "zero");
            var orgCas = result.Cas;

            // Update the value.  We shouldn't see a CAS error.

            result = await bucket.UpsertAsync("test", "one");

            var newCas = result.Cas;

            // Now attempt to update value using the old CAS.  This
            // should throw the CAS mismatch exception.

            var caught = (Exception)null;

            try
            {
                await bucket.UpsertSafeAsync("test", "two", cas: orgCas);
            }
            catch (Exception e)
            {
                caught = e;
            }

            Assert.NotNull(caught);
            Assert.True(CouchbaseTransientDetector.IsCasTransient(caught));
        }

        [Fact]
        public async Task DurabilityOverrides()
        {
            // Verify that the NeonBucket durability overrides work.  We're going
            // to be modifying the DEV_WORKSTATION environment variable so we
            // need to take care to restore its original value before the test
            // exist, to ensure that it will be correct for subsequent tests
            // running in the test runner process.

            var orgDevWorkstation = Environment.GetEnvironmentVariable("DEV_WORKSTATION");

            try
            {
                // Remove any existing DEV_WORKSTATION variable and verify that we
                // can explicitly specify the durability override.
                //
                // Note that the code below assumes that the Couchbase test fixture
                // creates a single node cluster.

                Environment.SetEnvironmentVariable("DEV_WORKSTATION", null);

                using (var bucket = fixture.Settings.OpenBucket(username, password, ignoreDurability: false))
                {
                    // Verify that we can ensure durability to one node.

                    
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("DEV_WORKSTATION", orgDevWorkstation);
            }
        }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    Test_CouchbaseFixture.cs
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
    /// <summary>
    /// Verify]ies that we can launch a Docker container fixture during tests.
    /// </summary>
    public class Test_CouchbaseFixture : IClassFixture<CouchbaseFixture>
    {
        private CouchbaseFixture    fixture;
        private NeonBucket          bucket;

        public Test_CouchbaseFixture(CouchbaseFixture fixture)
        {
            this.fixture = fixture;

            fixture.Initialize(
                () =>
                {
                    bucket = fixture.Start();
                });
        }

        /// <summary>
        /// Verify that we can access the Couchbase bucket.
        /// </summary>
        [Fact]
        public async Task VerifyAsync()
        {
            await bucket.UpsertSafeAsync("hello", "world!");

            Assert.Equal("world!", await bucket.GetSafeAsync<string>("hello"));
        }
    }
}

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

using Neon.Common;

using Xunit;

namespace TestCouchbase
{
    // Verify that we can launch a Docker container fixture during tests.

    public class Test_CouchbaseFixture : IClassFixture<CouchbaseFixture>
    {
        private CouchbaseFixture fixture;

        public Test_CouchbaseFixture(CouchbaseFixture fixture)
        {
            this.fixture = fixture;

            // Have the Docker container fixture launch the Alpine image and
            // sleep for a (long) while.

            fixture.StartCouchbase();
        }

        /// <summary>
        /// Verify that we can access the Couchbase bucket.
        /// </summary>
        [Fact]
        public async Task VerifyAsync()
        {
            await fixture.Bucket.UpsertAsync("hello", "world!");
        }
    }
}

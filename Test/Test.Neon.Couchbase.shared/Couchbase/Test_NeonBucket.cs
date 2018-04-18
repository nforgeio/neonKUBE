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
    public class Test_NeonBucket : IClassFixture<DockerContainerFixture>
    {
        public Test_NeonBucket(DockerContainerFixture fixture)
        {
        }
    }
}

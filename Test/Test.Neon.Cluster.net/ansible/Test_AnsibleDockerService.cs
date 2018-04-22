//-----------------------------------------------------------------------------
// FILE:	    Test_NeonCliAnsibleDockerService.cs
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

namespace TestNeonCluster
{
    public class Test_NeonCliAnsibleDockerService : IClassFixture<DockerFixture>
    {
        private DockerFixture docker;

        public Test_NeonCliAnsibleDockerService(DockerFixture docker)
        {
            this.docker = docker;
        }

        [Fact]
        public void Test()
        {
        }
    }
}

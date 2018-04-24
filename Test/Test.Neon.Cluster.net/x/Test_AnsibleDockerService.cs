//-----------------------------------------------------------------------------
// FILE:	    Test_AnsibleDockerService.cs
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
using Xunit.Neon;

namespace TestNeonCluster
{
    public class Test_AnsibleDockerService : IClassFixture<DockerFixture>
    {
        private DockerFixture docker;

        public Test_AnsibleDockerService(DockerFixture docker)
        {
            this.docker = docker;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Test()
        {
        }
    }
}

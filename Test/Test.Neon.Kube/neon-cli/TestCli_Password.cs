//-----------------------------------------------------------------------------
// FILE:	    TestCli_Password.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Kube;
using Neon.IO;
using Neon.Xunit;
using Neon.Xunit.Kube;

using Xunit;

namespace TestKube
{
    /// <summary>
    /// Tests <b>neon passwords</b> commands.fs
    /// </summary>
    public class TestCli_Password
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Password()
        {
            using (new KubeMock())
            {
                var response = TestHelper.Neon("password");

                Assert.Equal(0, response.ExitCode);
            }
        }
    }
}


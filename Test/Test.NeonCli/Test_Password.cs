//-----------------------------------------------------------------------------
// FILE:	    Test_Password.cs
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

namespace Test.NeonCli
{
    /// <summary>
    /// Tests <b>neon passwords</b> commands.fs
    /// </summary>
    public class Test_Password
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCli)]
        public void Password()
        {
            using (new KubeTestManager())
            {
                // Verify that [neon password] returns help/usage text.

                var response = KubeTestHelper.Neon("password");

                Assert.Equal(0, response.ExitCode);
                Assert.Contains("Manages neonKUBE passwords.", response.OutputText);

                // Verify that the "--help" option does the same thing.
 
                response = KubeTestHelper.Neon("password --help");

                Assert.Equal(0, response.ExitCode);
                Assert.Contains("Manages neonKUBE passwords.", response.OutputText);
            }
        }
    }
}


//-----------------------------------------------------------------------------
// FILE:	    Test_CommandBundle.cs
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

namespace TestHive
{
    public class Test_CommandBundle
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonHive)]
        public void ToBash_ArgWhitespace()
        {
            var bundle = new CommandBundle("test", "arg 1");

            var bash = bundle.ToBash();

            Assert.Equal(
@"test \
    ""arg 1""
", bash);
        }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    Test_Win32.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Windows;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_Win32
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void GetPhysicallyInstalledSystemMemory()
        {
            Assert.True(Win32.GetPhysicallyInstalledSystemMemory(out var memKB));
            Assert.True(memKB > NeonHelper.Mega);
        }
    }
}
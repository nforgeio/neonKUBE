//-----------------------------------------------------------------------------
// FILE:	    Test_PolledTimer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Time;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_PolledTimer
    {
        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public void Normal()
        {
            PolledTimer timer;
            DateTime    sysNow;

            timer = new PolledTimer(TimeSpan.FromSeconds(1.0));
            Assert.False(timer.HasFired);
            Thread.Sleep(2000);
            Assert.True(timer.HasFired);
            Assert.True(timer.HasFired);

            sysNow = SysTime.Now;
            timer.Reset();
            Assert.False(timer.HasFired);
            Assert.Equal(TimeSpan.FromSeconds(1.0), timer.Interval);
            Assert.True(timer.FireTime >= sysNow + timer.Interval);
            Thread.Sleep(2000);
            Assert.True(timer.HasFired);
            Assert.True(timer.HasFired);
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public async Task Async()
        {
            var timer  = new PolledTimer(TimeSpan.FromSeconds(1.0));
            var sysNow = SysTime.Now;

            timer.Reset();
            Assert.False(timer.HasFired);

            await timer.WaitAsync(TimeSpan.FromMilliseconds(500));

            Assert.True(timer.HasFired);
            Assert.True(SysTime.Now + TimeSpan.FromMilliseconds(50) - sysNow > TimeSpan.FromSeconds(1));
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public void ResetImmediate()
        {
            PolledTimer timer;
            DateTime    sysNow;

            timer = new PolledTimer(TimeSpan.FromSeconds(1.0));
            timer.ResetImmediate();
            Assert.True(timer.HasFired);
            Assert.True(timer.HasFired);

            sysNow = SysTime.Now;
            timer.Reset();
            Assert.Equal(TimeSpan.FromSeconds(1.0), timer.Interval);
            Assert.True(timer.FireTime >= sysNow + timer.Interval);
            Thread.Sleep(2000);
            Assert.True(timer.HasFired);
            Assert.True(timer.HasFired);
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public void AutoReset()
        {
            PolledTimer timer;
            DateTime    sysNow;

            sysNow = SysTime.Now;
            timer = new PolledTimer(TimeSpan.FromSeconds(1.0), true);
            Assert.False(timer.HasFired);
            Assert.Equal(TimeSpan.FromSeconds(1.0), timer.Interval);
            Assert.True(timer.FireTime >= sysNow + timer.Interval);
            Thread.Sleep(2000);
            Assert.True(timer.HasFired);
            Assert.False(timer.HasFired);

            Thread.Sleep(2000);
            Assert.True(timer.HasFired);
            Assert.False(timer.HasFired);
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public void FireNow()
        {
            PolledTimer timer;

            timer = new PolledTimer(TimeSpan.FromSeconds(10), true);
            Assert.False(timer.HasFired);
            timer.FireNow();
            Assert.True(timer.HasFired);
        }

        [Fact]
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
        public void Disable()
        {
            PolledTimer timer;
            DateTime    sysNow;

            sysNow   = SysTime.Now;
            timer = new PolledTimer(TimeSpan.FromSeconds(1.0));
            Assert.False(timer.HasFired);
            Assert.Equal(TimeSpan.FromSeconds(1.0), timer.Interval);
            Assert.True(timer.FireTime >= sysNow + timer.Interval);
            Thread.Sleep(2000);
            timer.Disable();
            Assert.False(timer.HasFired);
            timer.Reset();
            Thread.Sleep(2000);
            Assert.True(timer.HasFired);

            sysNow = SysTime.Now;
            timer.Reset();
            Assert.False(timer.HasFired);
            Assert.Equal(TimeSpan.FromSeconds(1.0), timer.Interval);
            Assert.True(timer.FireTime >= sysNow + timer.Interval);
            Thread.Sleep(2000);
            timer.Disable();
            Assert.False(timer.HasFired);
            timer.Interval = TimeSpan.FromSeconds(1.0);
            Thread.Sleep(2000);
            Assert.True(timer.HasFired);
        }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    Test_RecurringTimer.cs
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

// $todo(jefflill): Need to add tests for MINUTE and QUARTERHOUR.

namespace TestCommon
{
    public class Test_RecurringTimer
    {
        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void RecurringTimer_Disabled()
        {
            RecurringTimer timer;

            timer = new RecurringTimer(RecurringTimerType.Disabled, TimeSpan.FromSeconds(1));
            Assert.False(timer.HasFired(new DateTime(2010, 10, 23, 10, 10, 0)));   // Should never fire when disabled
            Assert.False(timer.HasFired(new DateTime(2010, 10, 24, 9, 0, 0)));
            Assert.False(timer.HasFired(new DateTime(2010, 10, 24, 10, 1, 0)));
            Assert.False(timer.HasFired(new DateTime(2010, 10, 24, 10, 1, 0)));
            Assert.False(timer.HasFired(new DateTime(2010, 10, 25, 10, 1, 0)));

            Assert.Equal("Disabled", timer.ToString());
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void RecurringTimer_Hourly()
        {
            RecurringTimer timer;

            timer = new RecurringTimer(RecurringTimerType.Hourly, TimeSpan.FromMinutes(4));

            Assert.False(timer.HasFired(new DateTime(2011, 08, 20, 10, 0, 0)));    // Never fires on the first poll
            Assert.True(timer.HasFired(new DateTime(2011, 08, 20, 10, 5, 0)));
            Assert.False(timer.HasFired(new DateTime(2011, 08, 20, 11, 1, 0)));    // Still before offset
            Assert.False(timer.HasFired(new DateTime(2011, 08, 20, 11, 3, 0)));    // Still before offset
            Assert.True(timer.HasFired(new DateTime(2011, 08, 20, 11, 4, 0)));     // Right at offset
            Assert.False(timer.HasFired(new DateTime(2011, 08, 20, 11, 4, 0)));    // Doesn't fire until the next hour
            Assert.False(timer.HasFired(new DateTime(2011, 08, 20, 11, 15, 0)));
            Assert.False(timer.HasFired(new DateTime(2011, 08, 20, 11, 30, 0)));
            Assert.False(timer.HasFired(new DateTime(2011, 08, 20, 11, 55, 0)));
            Assert.False(timer.HasFired(new DateTime(2011, 08, 20, 12, 0, 0)));
            Assert.True(timer.HasFired(new DateTime(2011, 08, 20, 12, 5, 0)));     // Just past the next firing time

            Assert.Equal("Hourly:04:00", timer.ToString());

            timer = new RecurringTimer("Hourly:10:11");
            Assert.Equal("Hourly:10:11", timer.ToString());
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void RecurringTimer_Daily()
        {
            RecurringTimer timer;

            timer = new RecurringTimer(new TimeOfDay("10:00:00"));
            Assert.False(timer.HasFired(new DateTime(2011, 10, 23, 10, 10, 0)));   // Verify that we don't fire until we see the transition
            Assert.False(timer.HasFired(new DateTime(2011, 10, 24, 9, 0, 0)));     // Still before the scheduled time
            Assert.True(timer.HasFired(new DateTime(2011, 10, 24, 10, 1, 0)));     // Should have seen the transition
            Assert.False(timer.HasFired(new DateTime(2011, 10, 24, 10, 1, 0)));    // Should be false now because we already handled this time
            Assert.True(timer.HasFired(new DateTime(2011, 10, 25, 10, 1, 0)));     // Should fire for the next day

            timer = new RecurringTimer(RecurringTimerType.Daily, new TimeSpan(10, 0, 0));
            Assert.False(timer.HasFired(new DateTime(2011, 10, 23, 10, 10, 0)));   // Verify that we don't fire until we see the transition
            Assert.False(timer.HasFired(new DateTime(2011, 10, 24, 9, 0, 0)));     // Still before the scheduled time
            Assert.True(timer.HasFired(new DateTime(2011, 10, 24, 10, 1, 0)));     // Should have seen the transition
            Assert.False(timer.HasFired(new DateTime(2011, 10, 24, 10, 1, 0)));    // Should be false now because we already handled this time
            Assert.True(timer.HasFired(new DateTime(2011, 10, 25, 10, 1, 0)));     // Should fire for the next day

            Assert.Equal("Daily:10:00:00", timer.ToString());

            timer = new RecurringTimer("Daily:10:11:12");
            Assert.Equal("Daily:10:11:12", timer.ToString());
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void RecurringTimer_Interval()
        {
            RecurringTimer timer;

            timer = new RecurringTimer(RecurringTimerType.Interval, TimeSpan.FromSeconds(10));
            timer.Start(new DateTime(2011, 8, 26, 0, 0, 0));
            Assert.False(timer.HasFired(new DateTime(2011, 8, 26, 0, 0, 0)));
            Assert.False(timer.HasFired(new DateTime(2011, 8, 26, 0, 0, 9)));
            Assert.True(timer.HasFired(new DateTime(2011, 8, 26, 0, 0, 10)));
            Assert.False(timer.HasFired(new DateTime(2011, 8, 26, 0, 0, 19)));
            Assert.True(timer.HasFired(new DateTime(2011, 8, 26, 0, 0, 21)));

            Assert.Equal("Interval:00:00:10", timer.ToString());

            timer = new RecurringTimer("Interval:48:11:12");
            Assert.Equal("Interval:48:11:12", timer.ToString());
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public async Task RecurringTimer_Async()
        {
            var timer  = new RecurringTimer(RecurringTimerType.Interval, TimeSpan.FromSeconds(1));
            var sysNow = SysTime.Now;

            timer.Reset();
            Assert.False(timer.HasFired());
            await timer.WaitAsync(TimeSpan.FromMilliseconds(500));
            Assert.True(SysTime.Now + TimeSpan.FromMilliseconds(50) - sysNow > TimeSpan.FromSeconds(1));
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public async Task RecurringTimer_Set()
        {
            var timer = new RecurringTimer("Interval:00:00:05");

            Assert.False(timer.HasFired());

            timer.Set();
            Assert.True(timer.HasFired());
            Assert.False(timer.HasFired());

            var startUtc = DateTime.UtcNow;

            await timer.WaitAsync(TimeSpan.FromMilliseconds(50));

            Assert.True(DateTime.UtcNow - startUtc >= TimeSpan.FromSeconds(5));
        }

        [Fact]
        [Trait(TestTraits.Project, TestProject.NeonCommon)]
        public void RecurringTimer_Parse()
        {
            RecurringTimer timer;

            timer = new RecurringTimer("Disabled");
            Assert.Equal(RecurringTimerType.Disabled, timer.Type);

            timer = new RecurringTimer("Minute");
            Assert.Equal(RecurringTimerType.Minute, timer.Type);
            Assert.Equal(TimeSpan.Zero, timer.TimeOffset);

            timer = new RecurringTimer("QuarterHour");
            Assert.Equal(RecurringTimerType.QuarterHour, timer.Type);
            Assert.Equal(TimeSpan.Zero, timer.TimeOffset);

            timer = new RecurringTimer("QuarterHour:10");
            Assert.Equal(RecurringTimerType.QuarterHour, timer.Type);
            Assert.Equal(TimeSpan.FromMinutes(10), timer.TimeOffset);

            timer = new RecurringTimer("QuarterHour:10:11");
            Assert.Equal(RecurringTimerType.QuarterHour, timer.Type);
            Assert.Equal(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(11), timer.TimeOffset);

            timer = new RecurringTimer("Hourly");
            Assert.Equal(RecurringTimerType.Hourly, timer.Type);
            Assert.Equal(TimeSpan.Zero, timer.TimeOffset);

            timer = new RecurringTimer("Hourly:10");
            Assert.Equal(RecurringTimerType.Hourly, timer.Type);
            Assert.Equal(TimeSpan.FromMinutes(10), timer.TimeOffset);

            timer = new RecurringTimer("Hourly:10:11");
            Assert.Equal(RecurringTimerType.Hourly, timer.Type);
            Assert.Equal(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(11), timer.TimeOffset);

            timer = new RecurringTimer("Daily");
            Assert.Equal(RecurringTimerType.Daily, timer.Type);
            Assert.Equal("00:00:00", timer.TimeOffset.ToString());

            timer = new RecurringTimer("Daily:10:11");
            Assert.Equal(RecurringTimerType.Daily, timer.Type);
            Assert.Equal(TimeSpan.FromHours(10) + TimeSpan.FromMinutes(11), timer.TimeOffset);

            timer = new RecurringTimer("Daily:10:11:12");
            Assert.Equal(RecurringTimerType.Daily, timer.Type);
            Assert.Equal(TimeSpan.FromHours(10) + TimeSpan.FromMinutes(11) + TimeSpan.FromSeconds(12), timer.TimeOffset);

            timer = new RecurringTimer("Interval:10:11:12");
            Assert.Equal(RecurringTimerType.Interval, timer.Type);
            Assert.Equal(TimeSpan.FromHours(10) + TimeSpan.FromMinutes(11) + TimeSpan.FromSeconds(12), timer.TimeOffset);

            timer = new RecurringTimer("Interval:48:11:12");
            Assert.Equal(RecurringTimerType.Interval, timer.Type);
            Assert.Equal(TimeSpan.FromHours(48) + TimeSpan.FromMinutes(11) + TimeSpan.FromSeconds(12), timer.TimeOffset);
        }
    }
}

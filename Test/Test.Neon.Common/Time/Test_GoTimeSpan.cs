//-----------------------------------------------------------------------------
// FILE:	    Test_GoTimeSpan.cs
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
    [Trait(TestTrait.Category, TestArea.NeonCommon)]
    public class Test_GoTimeSpan
    {
        private bool AreClose(double v1, double v2)
        {
            // We're seeing some floating point precision issues in some of
            // the tests below so we'll use inexact checks instead.

            return Math.Abs(v1 - v2) < 0.0000001;
        }

        [Fact]
        public void Parse()
        {
            // Verify valid input.

            Assert.Equal(0.0, GoTimeSpan.Parse("0").TimeSpan.TotalHours);
            Assert.Equal(GoTimeSpan.MinValue.TimeSpan, GoTimeSpan.Parse("-2562047h47m16s854ms775us808ns").TimeSpan);
            Assert.Equal(GoTimeSpan.MaxValue.TimeSpan, GoTimeSpan.Parse("2562047h47m16s854ms775us807ns").TimeSpan);

            var max = GoTimeSpan.MaxValue.ToString();
            var min = GoTimeSpan.MinValue.ToString();

            Assert.Equal(10.0, GoTimeSpan.Parse("10h").TotalHours);
            Assert.Equal(10.0, GoTimeSpan.Parse("10H").TotalHours);
            Assert.Equal(10.0, GoTimeSpan.Parse("+10h").TotalHours);
            Assert.Equal(-10.0, GoTimeSpan.Parse("-10h").TotalHours);
            Assert.Equal(1.5, GoTimeSpan.Parse("1.5h").TotalHours);

            Assert.Equal(123.0, GoTimeSpan.Parse("123m").TotalMinutes);
            Assert.Equal(123.0, GoTimeSpan.Parse("123M").TotalMinutes);
            Assert.Equal(123.0, GoTimeSpan.Parse("+123m").TotalMinutes);
            Assert.Equal(-123.0, GoTimeSpan.Parse("-123m").TotalMinutes);
            Assert.Equal(1.6, GoTimeSpan.Parse("1.6m").TotalMinutes);

            Assert.Equal(5.0, GoTimeSpan.Parse("5s").TotalSeconds);
            Assert.Equal(5.0, GoTimeSpan.Parse("5S").TotalSeconds);
            Assert.Equal(5.0, GoTimeSpan.Parse("+5s").TotalSeconds);
            Assert.Equal(-5.0, GoTimeSpan.Parse("-5s").TotalSeconds);
            Assert.Equal(1.7, GoTimeSpan.Parse("1.7s").TotalSeconds);

            Assert.Equal(5.0, GoTimeSpan.Parse("5ms").TotalMilliseconds);
            Assert.Equal(5.0, GoTimeSpan.Parse("5MS").TotalMilliseconds);
            Assert.Equal(5.0, GoTimeSpan.Parse("+5ms").TotalMilliseconds);
            Assert.Equal(-5.0, GoTimeSpan.Parse("-5ms").TotalMilliseconds);
            Assert.True(AreClose(1.7, GoTimeSpan.Parse("1.7ms").TotalMilliseconds));

            Assert.Equal(5.0, GoTimeSpan.Parse("5us").TotalMilliseconds * 1000);
            Assert.Equal(5.0, GoTimeSpan.Parse("5US").TotalMilliseconds * 1000);
            Assert.Equal(5.0, GoTimeSpan.Parse("+5us").TotalMilliseconds * 1000);
            Assert.Equal(-5.0, GoTimeSpan.Parse("-5us").TotalMilliseconds * 1000);
            Assert.True(AreClose(1.7, GoTimeSpan.Parse("1.7us").TotalMilliseconds * 1000));

            Assert.Equal(5.0, GoTimeSpan.Parse("5µs").TotalMilliseconds * 1000);
            Assert.Equal(5.0, GoTimeSpan.Parse("5µS").TotalMilliseconds * 1000);
            Assert.Equal(5.0, GoTimeSpan.Parse("+5µs").TotalMilliseconds * 1000);
            Assert.Equal(-5.0, GoTimeSpan.Parse("-5µs").TotalMilliseconds * 1000);
            Assert.True(AreClose(1.7, GoTimeSpan.Parse("1.7µs").TotalMilliseconds * 1000));

            Assert.Equal(100, GoTimeSpan.Parse("100ns").Ticks);
            Assert.Equal(1000, GoTimeSpan.Parse("1000NS").Ticks);
            Assert.Equal(150, GoTimeSpan.Parse("+150ns").Ticks);
            Assert.Equal(-1000, GoTimeSpan.Parse("-1000ns").Ticks);
            Assert.Equal(1000, GoTimeSpan.Parse("1000.5ns").Ticks);          // No nanosecond fractions

            Assert.Equal(1000.5, GoTimeSpan.Parse("1000h30m").TotalHours);
            Assert.Equal(-1000.5, GoTimeSpan.Parse("-1000h30m").TotalHours);

            // Verify invalid inputs.

            GoTimeSpan gts;

            Assert.False(GoTimeSpan.TryParse(null, out gts));
            Assert.False(GoTimeSpan.TryParse(string.Empty, out gts));
            Assert.False(GoTimeSpan.TryParse("    ", out gts));
            Assert.False(GoTimeSpan.TryParse("10", out gts));
            Assert.False(GoTimeSpan.TryParse("1000000000h", out gts));      // Out of range
            Assert.False(GoTimeSpan.TryParse("-1000000000h", out gts));     // Out of range
        }

        [Fact]
        public void Stringify()
        {
            Assert.Equal("0", GoTimeSpan.Zero.ToString());
            Assert.Equal("-2562047h47m16s854ms775us808ns", GoTimeSpan.MinValue.ToString());
            Assert.Equal("2562047h47m16s854ms775us807ns", GoTimeSpan.MaxValue.ToString());

            Assert.Equal("10h", GoTimeSpan.Parse("10h").ToString());
            Assert.Equal("10h30m", GoTimeSpan.Parse("10.5h").ToString());

            Assert.Equal("10m", GoTimeSpan.Parse("10m").ToString());
            Assert.Equal("10m30s", GoTimeSpan.Parse("10.5m").ToString());

            Assert.Equal("10s", GoTimeSpan.Parse("10s").ToString());
            Assert.Equal("10s500ms", GoTimeSpan.Parse("10.5s").ToString());

            Assert.Equal("10ms", GoTimeSpan.Parse("10ms").ToString());
            Assert.Equal("10ms500us", GoTimeSpan.Parse("10.5ms").ToString());

            Assert.Equal("1us", GoTimeSpan.Parse("1us").ToString());
            Assert.Equal("10us", GoTimeSpan.Parse("10us").ToString());
            Assert.Equal("10us500ns", GoTimeSpan.Parse("10.5us").ToString());

            Assert.Equal("100ns", GoTimeSpan.Parse("100ns").ToString());
            Assert.Equal("1us500ns", GoTimeSpan.Parse("1500ns").ToString());

            Assert.Equal("1000h30m", GoTimeSpan.Parse("1000h30m").ToString());
            Assert.Equal("-1000h30m", GoTimeSpan.Parse("-1000h30m").ToString());
        }

        [Fact]
        public void Casting()
        {
            TimeSpan ts = GoTimeSpan.FromTimeSpan(TimeSpan.FromHours(10));

            Assert.Equal(10.0, ts.TotalHours);

            GoTimeSpan gts = ts;

            Assert.Equal(10.0, gts.TimeSpan.TotalHours);

            // Verify out-of-range checks.

            Assert.Throws<ArgumentOutOfRangeException>(() => GoTimeSpan.FromTimeSpan(TimeSpan.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => GoTimeSpan.FromTimeSpan(TimeSpan.FromDays(500 * 365)));

            Assert.Throws<ArgumentOutOfRangeException>(() => GoTimeSpan.FromTimeSpan(TimeSpan.MinValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => GoTimeSpan.FromTimeSpan(-TimeSpan.FromDays(500 * 365)));
        }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    Test_GoDuration.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Time;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    [Trait(TestTrait.Category, TestArea.NeonCommon)]
    public class Test_GoDuration
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

            Assert.Equal(0.0, GoDuration.Parse("0").TimeSpan.TotalHours);
            Assert.Equal(GoDuration.MinValue.TimeSpan, GoDuration.Parse("-2562047h47m16s854ms775us808ns").TimeSpan);
            Assert.Equal(GoDuration.MaxValue.TimeSpan, GoDuration.Parse("2562047h47m16s854ms775us807ns").TimeSpan);

            var max = GoDuration.MaxValue.ToString();
            var min = GoDuration.MinValue.ToString();

            Assert.Equal(10.0, GoDuration.Parse("10h").TotalHours);
            Assert.Equal(10.0, GoDuration.Parse("10H").TotalHours);
            Assert.Equal(10.0, GoDuration.Parse("+10h").TotalHours);
            Assert.Equal(-10.0, GoDuration.Parse("-10h").TotalHours);
            Assert.Equal(1.5, GoDuration.Parse("1.5h").TotalHours);

            Assert.Equal(123.0, GoDuration.Parse("123m").TotalMinutes);
            Assert.Equal(123.0, GoDuration.Parse("123M").TotalMinutes);
            Assert.Equal(123.0, GoDuration.Parse("+123m").TotalMinutes);
            Assert.Equal(-123.0, GoDuration.Parse("-123m").TotalMinutes);
            Assert.Equal(1.6, GoDuration.Parse("1.6m").TotalMinutes);

            Assert.Equal(5.0, GoDuration.Parse("5s").TotalSeconds);
            Assert.Equal(5.0, GoDuration.Parse("5S").TotalSeconds);
            Assert.Equal(5.0, GoDuration.Parse("+5s").TotalSeconds);
            Assert.Equal(-5.0, GoDuration.Parse("-5s").TotalSeconds);
            Assert.Equal(1.7, GoDuration.Parse("1.7s").TotalSeconds);

            Assert.Equal(5.0, GoDuration.Parse("5ms").TotalMilliseconds);
            Assert.Equal(5.0, GoDuration.Parse("5MS").TotalMilliseconds);
            Assert.Equal(5.0, GoDuration.Parse("+5ms").TotalMilliseconds);
            Assert.Equal(-5.0, GoDuration.Parse("-5ms").TotalMilliseconds);
            Assert.True(AreClose(1.7, GoDuration.Parse("1.7ms").TotalMilliseconds));

            Assert.Equal(5.0, GoDuration.Parse("5us").TotalMilliseconds * 1000);
            Assert.Equal(5.0, GoDuration.Parse("5US").TotalMilliseconds * 1000);
            Assert.Equal(5.0, GoDuration.Parse("+5us").TotalMilliseconds * 1000);
            Assert.Equal(-5.0, GoDuration.Parse("-5us").TotalMilliseconds * 1000);
            Assert.True(AreClose(1.7, GoDuration.Parse("1.7us").TotalMilliseconds * 1000));

            Assert.Equal(5.0, GoDuration.Parse("5µs").TotalMilliseconds * 1000);
            Assert.Equal(5.0, GoDuration.Parse("5µS").TotalMilliseconds * 1000);
            Assert.Equal(5.0, GoDuration.Parse("+5µs").TotalMilliseconds * 1000);
            Assert.Equal(-5.0, GoDuration.Parse("-5µs").TotalMilliseconds * 1000);
            Assert.True(AreClose(1.7, GoDuration.Parse("1.7µs").TotalMilliseconds * 1000));

            Assert.Equal(100, GoDuration.Parse("100ns").Ticks);
            Assert.Equal(1000, GoDuration.Parse("1000NS").Ticks);
            Assert.Equal(150, GoDuration.Parse("+150ns").Ticks);
            Assert.Equal(-1000, GoDuration.Parse("-1000ns").Ticks);
            Assert.Equal(1000, GoDuration.Parse("1000.5ns").Ticks);          // No nanosecond fractions

            Assert.Equal(1000.5, GoDuration.Parse("1000h30m").TotalHours);
            Assert.Equal(-1000.5, GoDuration.Parse("-1000h30m").TotalHours);

            // Verify invalid inputs.

            GoDuration gts;

            Assert.False(GoDuration.TryParse(null, out gts));
            Assert.False(GoDuration.TryParse(string.Empty, out gts));
            Assert.False(GoDuration.TryParse("    ", out gts));
            Assert.False(GoDuration.TryParse("10", out gts));
            Assert.False(GoDuration.TryParse("1000000000h", out gts));      // Out of range
            Assert.False(GoDuration.TryParse("-1000000000h", out gts));     // Out of range
        }

        [Fact]
        public void Stringify()
        {
            Assert.Equal("0", GoDuration.Zero.ToString());
            Assert.Equal("-2562047h47m16s854ms775us808ns", GoDuration.MinValue.ToString());
            Assert.Equal("2562047h47m16s854ms775us807ns", GoDuration.MaxValue.ToString());

            Assert.Equal("10h", GoDuration.Parse("10h").ToString());
            Assert.Equal("10h30m", GoDuration.Parse("10.5h").ToString());

            Assert.Equal("10m", GoDuration.Parse("10m").ToString());
            Assert.Equal("10m30s", GoDuration.Parse("10.5m").ToString());

            Assert.Equal("10s", GoDuration.Parse("10s").ToString());
            Assert.Equal("10s500ms", GoDuration.Parse("10.5s").ToString());

            Assert.Equal("10ms", GoDuration.Parse("10ms").ToString());
            Assert.Equal("10ms500us", GoDuration.Parse("10.5ms").ToString());

            Assert.Equal("1us", GoDuration.Parse("1us").ToString());
            Assert.Equal("10us", GoDuration.Parse("10us").ToString());
            Assert.Equal("10us500ns", GoDuration.Parse("10.5us").ToString());

            Assert.Equal("100ns", GoDuration.Parse("100ns").ToString());
            Assert.Equal("1us500ns", GoDuration.Parse("1500ns").ToString());

            Assert.Equal("1000h30m", GoDuration.Parse("1000h30m").ToString());
            Assert.Equal("-1000h30m", GoDuration.Parse("-1000h30m").ToString());

            //-----------------------------------------------------------------
            // Pretty

            Assert.Equal("0", GoDuration.Parse("0").ToPretty());
            Assert.Equal("0", GoDuration.Parse("-0s").ToPretty());

            Assert.Equal("0.123s", GoDuration.Parse("0.123s").ToPretty());
            Assert.Equal("-0.123s", GoDuration.Parse("-0.123s").ToPretty());

            Assert.Equal("10s", GoDuration.Parse("10s").ToPretty());
            Assert.Equal("-10s", GoDuration.Parse("-10s").ToPretty());

            Assert.Equal("10m", GoDuration.Parse("10m").ToPretty());
            Assert.Equal("-10m", GoDuration.Parse("-10m").ToPretty());

            Assert.Equal("10h", GoDuration.Parse("10h").ToPretty());
            Assert.Equal("-10h", GoDuration.Parse("-10h").ToPretty());

            Assert.Equal("20m10s", GoDuration.Parse("20m10s").ToPretty());
            Assert.Equal("-20m10s", GoDuration.Parse("-20m10s").ToPretty());

            Assert.Equal("20m10.123s", GoDuration.Parse("20m10.123s").ToPretty());
            Assert.Equal("-20m10.123s", GoDuration.Parse("-20m10.123s").ToPretty());

            Assert.Equal("2h20m10s", GoDuration.Parse("2h20m10s").ToPretty());
            Assert.Equal("-2h20m10s", GoDuration.Parse("-2h20m10s").ToPretty());

            Assert.Equal("2h20m10.123s", GoDuration.Parse("2h20m10.123s").ToPretty());
            Assert.Equal("-2h20m10.123s", GoDuration.Parse("-2h20m10.123s").ToPretty());

            Assert.Equal("2h20m0.123s", GoDuration.Parse("2h20m0.123s").ToPretty());
            Assert.Equal("-2h20m0.123s", GoDuration.Parse("-2h20m0.123s").ToPretty());

            Assert.Equal("1h30m", GoDuration.Parse("1h30m").ToPretty());
            Assert.Equal("-1h30m", GoDuration.Parse("-1h30m").ToPretty());

            Assert.Equal("1h30s", GoDuration.Parse("1h30s").ToPretty());
            Assert.Equal("-1h30s", GoDuration.Parse("-1h30s").ToPretty());

            Assert.Equal("1h0.123s", GoDuration.Parse("1h0.123s").ToPretty());
            Assert.Equal("-1h0.123s", GoDuration.Parse("-1h0.123s").ToPretty());
        }

        [Fact]
        public void Casting()
        {
            TimeSpan ts = GoDuration.FromTimeSpan(TimeSpan.FromHours(10));

            Assert.Equal(10.0, ts.TotalHours);

            GoDuration gts = ts;

            Assert.Equal(10.0, gts.TimeSpan.TotalHours);

            // Verify out-of-range checks.

            Assert.Throws<ArgumentOutOfRangeException>(() => GoDuration.FromTimeSpan(TimeSpan.MaxValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => GoDuration.FromTimeSpan(TimeSpan.FromDays(500 * 365)));

            Assert.Throws<ArgumentOutOfRangeException>(() => GoDuration.FromTimeSpan(TimeSpan.MinValue));
            Assert.Throws<ArgumentOutOfRangeException>(() => GoDuration.FromTimeSpan(-TimeSpan.FromDays(500 * 365)));
        }

        [Fact]
        public void RegEx()
        {
            // Verify the GOLANG duration REGEX pattern.

            //-----------------------------------------------------------------
            // MATCH verification

            // Nanoseconds

            Assert.Matches(GoDuration.RegEx, "0ns");
            Assert.Matches(GoDuration.RegEx, "0.123ns");
            Assert.Matches(GoDuration.RegEx, "123.456ns");

            // Microseconds

            Assert.Matches(GoDuration.RegEx, "0µs");
            Assert.Matches(GoDuration.RegEx, "0.123µs");
            Assert.Matches(GoDuration.RegEx, "123.456µs");

            Assert.Matches(GoDuration.RegEx, "0us");
            Assert.Matches(GoDuration.RegEx, "0.123us");
            Assert.Matches(GoDuration.RegEx, "123.456us");

            // Milliseconds

            Assert.Matches(GoDuration.RegEx, "0ms");
            Assert.Matches(GoDuration.RegEx, "0.123ms");
            Assert.Matches(GoDuration.RegEx, "123.456ms");

            // Seconds

            Assert.Matches(GoDuration.RegEx, "0");
            Assert.Matches(GoDuration.RegEx, "0s");
            Assert.Matches(GoDuration.RegEx, "0.123s");
            Assert.Matches(GoDuration.RegEx, "123.456s");

            // Minutes

            Assert.Matches(GoDuration.RegEx, "0m");
            Assert.Matches(GoDuration.RegEx, "0.123m");
            Assert.Matches(GoDuration.RegEx, "123.456m");

            // Hours

            Assert.Matches(GoDuration.RegEx, "0h");
            Assert.Matches(GoDuration.RegEx, "0.123h");
            Assert.Matches(GoDuration.RegEx, "123.456h");

            // Combinations

            Assert.Matches(GoDuration.RegEx, "1h2m3s4ms5µs6ns");
            Assert.Matches(GoDuration.RegEx, "1h2m3s4ms5µs6ns");

            Assert.Matches(GoDuration.RegEx, "1h6ns");
            Assert.Matches(GoDuration.RegEx, "1h5µs6ns");
            Assert.Matches(GoDuration.RegEx, "1h3s4ms5µs6ns");
            Assert.Matches(GoDuration.RegEx, "1h2m3s4ms5µs6ns");

            Assert.Matches(GoDuration.RegEx, "1m6ns");
            Assert.Matches(GoDuration.RegEx, "1m5µs6ns");
            Assert.Matches(GoDuration.RegEx, "1m3s4ms5µs6ns");

            Assert.Matches(GoDuration.RegEx, "1s6ns");
            Assert.Matches(GoDuration.RegEx, "1s5µs6ns");
            Assert.Matches(GoDuration.RegEx, "3s4ms5µs6ns");

            Assert.Matches(GoDuration.RegEx, "1ms6ns");
            Assert.Matches(GoDuration.RegEx, "1ms5µs6ns");
            Assert.Matches(GoDuration.RegEx, "1ms5µs6ns");

            Assert.Matches(GoDuration.RegEx, "5µs6ns");

            Assert.Matches(GoDuration.RegEx, "1.0h2.0m3.0s4.0ms5.0us6.0ns");
            Assert.Matches(GoDuration.RegEx, "1.0h2.0m3.0s4.0ms5.0µs6.0ns");

            //-----------------------------------------------------------------
            // NO-MATCH verification

            Assert.DoesNotMatch(GoDuration.RegEx, "");          // <-- empty string not valid
            Assert.DoesNotMatch(GoDuration.RegEx, "10x0");      // <-- missing unit
            Assert.DoesNotMatch(GoDuration.RegEx, "x");         // <-- invalid character
            Assert.DoesNotMatch(GoDuration.RegEx, "10y");       // <-- invalid unit
            Assert.DoesNotMatch(GoDuration.RegEx, "1h1h");      // <-- duplicate units
            Assert.DoesNotMatch(GoDuration.RegEx, "1s2h");      // <-- out of order units
            Assert.DoesNotMatch(GoDuration.RegEx, "1.h");       // <-- improper decimal part
        }
    }
}

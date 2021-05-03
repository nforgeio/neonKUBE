//-----------------------------------------------------------------------------
// FILE:	    Test_TimeSpan.cs
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
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Retry;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_TimeSpan
    {
        [Fact]
        [Trait(TestTrait.Area, TestArea.NeonCommon)]
        public void AdjustToFitDateRange()
        {
            // Verify that we can adjust a timespan such that when
            // added to a DateTime, the result won't exceed the
            // max/min possible dates.

            // TimeSpan.Zero should always work without adjustment.

            Assert.Equal(TimeSpan.Zero, TimeSpan.Zero.AdjustToFitDateRange(default));
            Assert.Equal(TimeSpan.Zero, TimeSpan.Zero.AdjustToFitDateRange(DateTime.MinValue));
            Assert.Equal(TimeSpan.Zero, TimeSpan.Zero.AdjustToFitDateRange(DateTime.MaxValue));

            // Verify timespans that won't exceed the data range.

            var timespan = TimeSpan.FromDays(10);
            var datetime = new DateTime(2019, 4, 28);

            Assert.Equal(timespan, timespan.AdjustToFitDateRange(datetime));
            Assert.Equal(timespan, timespan.AdjustToFitDateRange(DateTime.MaxValue - timespan));
            Assert.Equal(timespan, timespan.AdjustToFitDateRange(DateTime.MinValue + timespan));

            // Verify a timespan that breaks the lower date range.

            timespan = -TimeSpan.FromDays(10);

            Assert.Equal(timespan + TimeSpan.FromDays(1), timespan.AdjustToFitDateRange(DateTime.MinValue - timespan - TimeSpan.FromDays(1)));

            // Verify a timespan that breaks the upper date range.

            timespan = TimeSpan.FromDays(10);

            Assert.Equal(timespan - TimeSpan.FromDays(1), timespan.AdjustToFitDateRange(DateTime.MaxValue - timespan + TimeSpan.FromDays(1)));
        }
    }
}

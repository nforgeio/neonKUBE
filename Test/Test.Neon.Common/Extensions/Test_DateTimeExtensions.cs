//-----------------------------------------------------------------------------
// FILE:	    Test_DateTimeExtensions.cs
// CONTRIBUTOR: Marcus Bowyer
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
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Collections;
using Neon.Net;
using Neon.Retry;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    [Trait(TestTrait.Category, TestArea.NeonCommon)]
    public class Test_DateTimeExtensions
    {
        [Fact]
        public void RoundUp()
        {
            Assert.Throws<ArgumentException>(() => DateTime.UtcNow.RoundUp(TimeSpan.FromMinutes(-1)));
            Assert.Throws<ArgumentException>(() => DateTime.UtcNow.RoundUp(TimeSpan.FromMinutes(0)));
            Assert.Throws<ArgumentException>(() => DateTime.UtcNow.RoundUp(TimeSpan.FromSeconds(0)));

            var date = new DateTime(2010, 02, 05, 10, 35, 25, 450); // 2010/02/05 10:35:25.450
            var roundedUp = date.RoundUp(TimeSpan.FromMinutes(1));  // 2010/02/05 10:36:00.000
            Assert.Equal(new DateTime(2010, 02, 05, 10, 36, 0, 0), roundedUp);

            date = new DateTime(2010, 02, 05, 10, 35, 25, 450); // 2010/02/05 10:35:25.450
            roundedUp = date.RoundUp(TimeSpan.FromMinutes(15)); // 2010/02/05 10:45:00.000
            Assert.Equal(new DateTime(2010, 02, 05, 10, 45, 0, 0), roundedUp);

            date = new DateTime(2010, 02, 05, 10, 35, 25, 450); // 2010/02/05 10:35:25.450
            roundedUp = date.RoundUp(TimeSpan.FromSeconds(1));  // 2010/02/05 10:36:00.000
            Assert.Equal(new DateTime(2010, 02, 05, 10, 35, 26, 0), roundedUp);
        }

        [Fact]
        public void RoundDown()
        {
            Assert.Throws<ArgumentException>(() => DateTime.UtcNow.RoundDown(TimeSpan.FromMinutes(-1)));
            Assert.Throws<ArgumentException>(() => DateTime.UtcNow.RoundDown(TimeSpan.FromMinutes(0)));
            Assert.Throws<ArgumentException>(() => DateTime.UtcNow.RoundDown(TimeSpan.FromSeconds(0)));

            var date = new DateTime(2010, 02, 05, 10, 35, 25, 450);    // 2010/02/05 10:35:25.450
            var roundedDown = date.RoundDown(TimeSpan.FromMinutes(1)); // 2010/02/05 10:35:00.000
            Assert.Equal(new DateTime(2010, 02, 05, 10, 35, 0, 0), roundedDown);

            date = new DateTime(2010, 02, 05, 10, 35, 25, 450);     // 2010/02/05 10:35:25.450
            roundedDown = date.RoundDown(TimeSpan.FromMinutes(15)); // 2010/02/05 10:30:00.000
            Assert.Equal(new DateTime(2010, 02, 05, 10, 30, 0, 0), roundedDown);

            date = new DateTime(2010, 02, 05, 10, 35, 25, 450);     // 2010/02/05 10:35:25.450
            roundedDown = date.RoundDown(TimeSpan.FromSeconds(1));  // 2010/02/05 10:36:00.000
            Assert.Equal(new DateTime(2010, 02, 05, 10, 35, 25, 0), roundedDown);
        }

        [Fact]
        public void RoundToNearest()
        {
            Assert.Throws<ArgumentException>(() => DateTime.UtcNow.RoundToNearest(TimeSpan.FromMinutes(-1)));
            Assert.Throws<ArgumentException>(() => DateTime.UtcNow.RoundToNearest(TimeSpan.FromMinutes(0)));
            Assert.Throws<ArgumentException>(() => DateTime.UtcNow.RoundToNearest(TimeSpan.FromSeconds(0)));

            var date = new DateTime(2010, 02, 05, 10, 35, 25, 450);     // 2010/02/05 10:35:25.450
            var rounded = date.RoundToNearest(TimeSpan.FromMinutes(1)); // 2010/02/05 10:35:00.000
            Assert.Equal(new DateTime(2010, 02, 05, 10, 35, 0, 0), rounded);

            date = new DateTime(2010, 02, 05, 10, 35, 25, 450);      // 2010/02/05 10:35:25.450
            rounded = date.RoundToNearest(TimeSpan.FromMinutes(15)); // 2010/02/05 10:30:00.000
            Assert.Equal(new DateTime(2010, 02, 05, 10, 30, 0, 0), rounded);

            date = new DateTime(2010, 02, 05, 10, 35, 25, 450);     // 2010/02/05 10:35:25.450
            rounded = date.RoundToNearest(TimeSpan.FromSeconds(1)); // 2010/02/05 10:35:25.000
            Assert.Equal(new DateTime(2010, 02, 05, 10, 35, 25, 0), rounded);

            date = new DateTime(2010, 02, 05, 10, 35, 25, 500);     // 2010/02/05 10:35:25.500
            rounded = date.RoundToNearest(TimeSpan.FromSeconds(1)); // 2010/02/05 10:35:26.000
            Assert.Equal(new DateTime(2010, 02, 05, 10, 35, 26, 0), rounded);

            date = new DateTime(2010, 02, 05, 10, 35, 37, 500);     // 2010/02/05 10:35:37.500
            rounded = date.RoundToNearest(TimeSpan.FromSeconds(5)); // 2010/02/05 10:35:40.000
            Assert.Equal(new DateTime(2010, 02, 05, 10, 35, 40, 0), rounded);

            date = new DateTime(2010, 02, 05, 10, 35, 37, 500);     // 2010/02/05 10:35:37.500
            rounded = date.RoundToNearest(TimeSpan.FromSeconds(1)); // 2010/02/05 10:35:38.000
            Assert.Equal(new DateTime(2010, 02, 05, 10, 35, 38, 0), rounded);
        }
    }
}

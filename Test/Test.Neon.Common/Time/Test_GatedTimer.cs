//-----------------------------------------------------------------------------
// FILE:	    Test_GatedTimer.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

// $todo(jefflill):
//
// This is very old code that could be simplified using the
// C# => operator and closures.

namespace TestCommon
{
    public class Test_GatedTimer
    {
        GatedTimer      timer;
        private int     wait;
        private int     count;
        private int     maxCount;
        private object  state;
        private bool    dispose;
        private int     change;

        private void OnTimer(object state)
        {
            if (count < maxCount)
            {
                count++;
            }

            this.state = state;

            Thread.Sleep(wait);

            if (dispose)
            {
                timer.Dispose();
            }

            if (change > 0)
            {
                timer.Change(TimeSpan.Zero, TimeSpan.FromMilliseconds(change));
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Basic()
        {
            count    = 0;
            maxCount = int.MaxValue;
            state    = null;
            wait     = 2000;
            dispose  = false;
            change   = 0;
            timer    = new GatedTimer(new TimerCallback(OnTimer), 10, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

            Thread.Sleep(1000);
            timer.Dispose();
            Assert.Equal(1, count);
            Assert.Equal(10, (int)state);

            count    = 0;
            maxCount = 10;
            state    = null;
            wait     = 0;
            dispose  = false;
            change   = 0;
            timer    = new GatedTimer(new TimerCallback(OnTimer), 10, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

            Thread.Sleep(2000);
            timer.Dispose();
            Assert.Equal(10, count);
            Assert.Equal(10, (int)state);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Dispose()
        {
            count    = 0;
            maxCount = int.MaxValue;
            state    = null;
            wait     = 0;
            dispose  = true;
            change   = 0;
            timer    = new GatedTimer(new TimerCallback(OnTimer), 10, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));

            Thread.Sleep(1000);
            Assert.Equal(1, count);
            Assert.Equal(10, (int)state);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Change()
        {
            // $todo(jefflill): Need to implement this.
        }
    }
}
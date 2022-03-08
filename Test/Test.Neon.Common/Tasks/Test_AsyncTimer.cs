//-----------------------------------------------------------------------------
// FILE:	    Test_AsyncTimer.cs
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
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Retry;
using Neon.Tasks;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    [Trait(TestTrait.Category, TestArea.NeonCommon)]
    public class Test_AsyncTimer
    {
        [Fact]
        public async Task StartStop()
        {
            // Verify that basic start/stop operations work.

            var ticks = 0;

            using (var timer = new AsyncTimer(
                async () =>
                {
                    ticks++;
                    await Task.CompletedTask;
                }))
            {
                Assert.False(timer.IsRunning);
                await Task.Delay(TimeSpan.FromSeconds(1));
                Assert.False(timer.IsRunning);
                Assert.Equal(0, ticks);

                timer.Start(TimeSpan.FromSeconds(1));
                await Task.Delay(TimeSpan.FromSeconds(4.5));
                Assert.True(ticks == 5);

                timer.Stop();
                ticks = 0;
                await Task.Delay(TimeSpan.FromSeconds(4.5));
                Assert.True(ticks == 0);
            }
        }

        [Fact]
        public async Task StartStop_Delay()
        {
            var ticks = 0;

            // Verify that basic start/stop operations work when
            // delaying the first tick callback.

            using (var timer = new AsyncTimer(
                async () =>
                {
                    ticks++;
                    await Task.CompletedTask;
                }))
            {
                Assert.False(timer.IsRunning);
                await Task.Delay(TimeSpan.FromSeconds(1));
                Assert.False(timer.IsRunning);
                Assert.Equal(0, ticks);

                timer.Start(TimeSpan.FromSeconds(1), delayFirstTick: true);
                await Task.Delay(TimeSpan.FromSeconds(4.5));
                Assert.True(ticks == 4);

                timer.Stop();
                ticks = 0;
                await Task.Delay(TimeSpan.FromSeconds(4.5));
                Assert.True(ticks == 0);
            }
        }

        [Fact]
        public async Task Restart_SameInterval()
        {
            // Verify that we can restart a timer using the original interval.

            var ticks = 0;

            using (var timer = new AsyncTimer(
                async () =>
                {
                    ticks++;
                    await Task.CompletedTask;
                }))
            {
                Assert.False(timer.IsRunning);
                await Task.Delay(TimeSpan.FromSeconds(1));
                Assert.False(timer.IsRunning);
                Assert.Equal(0, ticks);

                timer.Start(TimeSpan.FromSeconds(1));
                await Task.Delay(TimeSpan.FromSeconds(4.5));
                Assert.True(ticks == 5);

                timer.Stop();
                ticks = 0;
                await Task.Delay(TimeSpan.FromSeconds(4.5));
                Assert.True(ticks == 0);

                timer.Start();
                await Task.Delay(TimeSpan.FromSeconds(4.5));
                Assert.True(ticks == 5);

                timer.Stop();
                ticks = 0;
                await Task.Delay(TimeSpan.FromSeconds(4.5));
                Assert.True(ticks == 0);
            }
        }

        [Fact]
        public async Task Restart_DifferentCallback()
        {
            // Verify that we can restart a timer using the original interval
            // and a different callback.

            var ticks0 = 0;
            var ticks1 = 0;

            using (var timer = new AsyncTimer(
                async () =>
                {
                    ticks0++;
                    await Task.CompletedTask;
                }))
            {
                Assert.False(timer.IsRunning);
                await Task.Delay(TimeSpan.FromSeconds(1));
                Assert.False(timer.IsRunning);
                Assert.Equal(0, ticks0);

                timer.Start(TimeSpan.FromSeconds(1));
                await Task.Delay(TimeSpan.FromSeconds(4.5));
                Assert.True(ticks0 == 5);

                timer.Stop();
                ticks0 = 0;
                await Task.Delay(TimeSpan.FromSeconds(4.5));
                Assert.True(ticks0 == 0);

                timer.Start(
                    callback: async () =>
                    {
                        ticks1++;
                        await Task.CompletedTask;
                    });
                await Task.Delay(TimeSpan.FromSeconds(4.5));
                Assert.True(ticks1 == 5);

                timer.Stop();
                ticks1 = 0;
                await Task.Delay(TimeSpan.FromSeconds(4.5));
                Assert.True(ticks1 == 0);
            }
        }

        [Fact]
        public async Task Restart_DifferentInterval()
        {
            // Verify that we can restart a timer using a different interval
            // and also delaying the first tick.

            var ticks = 0;

            using (var timer = new AsyncTimer(
                async () =>
                {
                    ticks++;
                    await Task.CompletedTask;
                }))
            {
                Assert.False(timer.IsRunning);
                await Task.Delay(TimeSpan.FromSeconds(1));
                Assert.False(timer.IsRunning);
                Assert.Equal(0, ticks);

                timer.Start(TimeSpan.FromSeconds(1));
                await Task.Delay(TimeSpan.FromSeconds(4.5));
                Assert.True(ticks == 5);

                timer.Stop();
                ticks = 0;
                await Task.Delay(TimeSpan.FromSeconds(4.5));
                Assert.True(ticks == 0);

                timer.Start(TimeSpan.FromSeconds(2), delayFirstTick: true);
                await Task.Delay(TimeSpan.FromSeconds(4.5));
                Assert.True(ticks == 2);

                timer.Stop();
                ticks = 0;
                await Task.Delay(TimeSpan.FromSeconds(4.5));
                Assert.True(ticks == 0);
            }
        }

        [Fact]
        public async Task Dispose()
        {
            // Verify that [Dispose()] stops the timer.

            var ticks = 0;

            var timer = new AsyncTimer(
                async () =>
                {
                    ticks++;
                    await Task.CompletedTask;
                });

            timer.Start(TimeSpan.FromSeconds(1));
            await Task.Delay(TimeSpan.FromSeconds(4.5));
            Assert.True(ticks == 5);

            timer.Dispose();
            ticks = 0;
            await Task.Delay(TimeSpan.FromSeconds(4.5));
            Assert.True(ticks == 0);

            // Verify that calling [Dispose()] on an already disposed timer
            // does not throw an exception.

            timer.Dispose();
        }

        [Fact]
        public async Task Callback_Exceptions()
        {
            // Verify that the timer continues to tick even when the callback
            // throws exceptions.

            var ticks = 0;

            using (var timer = new AsyncTimer(
                async () =>
                {
                    ticks++;
                    await Task.CompletedTask;
                    throw new Exception();
                }))
            {
                timer.Start(TimeSpan.FromSeconds(1));
                await Task.Delay(TimeSpan.FromSeconds(4.5));
                Assert.True(ticks == 5);
            }
        }

        [Fact]
        public void Errors()
        {
            // Check error detection.

            var timer = new AsyncTimer(async () => await Task.CompletedTask);

            Assert.Throws<ArgumentException>(() => timer.Start(TimeSpan.FromSeconds(-1)));
            Assert.Throws<InvalidOperationException>(() => timer.Start());

            timer.Dispose();

            Assert.Throws<ObjectDisposedException>(() => timer.Start(TimeSpan.FromSeconds(1)));
            Assert.Throws<ObjectDisposedException>(() => timer.Stop());

            timer = new AsyncTimer();

            Assert.Throws<InvalidOperationException>(() => timer.Start(TimeSpan.FromSeconds(1)));
            timer.Dispose();
        }
    }
}

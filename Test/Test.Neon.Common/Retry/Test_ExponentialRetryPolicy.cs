//-----------------------------------------------------------------------------
// FILE:	    Test_ExponentialRetryPolicy.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Threading.Tasks;

using Neon.Common;
using Neon.Retry;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_ExponentialRetryPolicy
    {
        private class TransientException : Exception
        {
        }

        private bool TransientDetector(Exception e)
        {
            return e is TransientException;
        }

        private bool VerifyInterval(DateTime time0, DateTime time1, TimeSpan minInterval)
        {
            // Verify that [time1] is greater than [time0] by at least [minInterval]
            // allowing 100ms of slop due to the fact that Task.Delay() sometimes 
            // delays for less than the requested timespan.

            return time1 - time0 > minInterval - TimeSpan.FromMilliseconds(100);
        }

        /// <summary>
        /// Verify that operation retry times are consistent with the retry policy.
        /// </summary>
        /// <param name="times"></param>
        /// <param name="policy"></param>
        private void VerifyIntervals(List<DateTime> times, ExponentialRetryPolicy policy)
        {
            var interval = policy.InitialRetryInterval;

            for (int i = 0; i < times.Count - 1; i++)
            {
                Assert.True(VerifyInterval(times[i], times[i + 1], interval));

                interval = TimeSpan.FromTicks(interval.Ticks * 2);

                if (interval > policy.MaxRetryInterval)
                {
                    interval = policy.MaxRetryInterval;
                }
            }
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void Defaults()
        {
            var policy = new ExponentialRetryPolicy(TransientDetector, sourceModule: "test");

            Assert.Equal(5, policy.MaxAttempts);
            Assert.Equal(TimeSpan.FromSeconds(1), policy.InitialRetryInterval);
            Assert.Equal(TimeSpan.FromHours(24), policy.MaxRetryInterval);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task FailAll()
        {
            var policy = new ExponentialRetryPolicy(TransientDetector);
            var times  = new List<DateTime>();

            await Assert.ThrowsAsync<TransientException>(
                async () =>
                {
                    await policy.InvokeAsync(
                        async () =>
                        {
                            times.Add(DateTime.UtcNow);
                            await Task.CompletedTask;
                            throw new TransientException();
                        });
                });

            Assert.Equal(policy.MaxAttempts , times.Count);
            VerifyIntervals(times, policy);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task FailAll_Result()
        {
            var policy = new ExponentialRetryPolicy(TransientDetector);
            var times  = new List<DateTime>();

            await Assert.ThrowsAsync<TransientException>(
                async () =>
                {
                    await policy.InvokeAsync<string>(
                        async () =>
                        {
                            times.Add(DateTime.UtcNow);
                            await Task.CompletedTask;
                            throw new TransientException();
                        });
                });

            Assert.Equal(policy.MaxAttempts, times.Count);
            VerifyIntervals(times, policy);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task FailImmediate()
        {
            var policy = new ExponentialRetryPolicy(TransientDetector);
            var times  = new List<DateTime>();

            await Assert.ThrowsAsync<NotImplementedException>(
                async () =>
                {
                    await policy.InvokeAsync(
                        async () =>
                        {
                            times.Add(DateTime.UtcNow);
                            await Task.CompletedTask;
                            throw new NotImplementedException();
                        });
                });

            Assert.Single(times);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task FailImmediate_Result()
        {
            var policy = new ExponentialRetryPolicy(TransientDetector);
            var times  = new List<DateTime>();

            await Assert.ThrowsAsync<NotImplementedException>(
                async () =>
                {
                    await policy.InvokeAsync<string>(
                        async () =>
                        {
                            times.Add(DateTime.UtcNow);
                            await Task.CompletedTask;
                            throw new NotImplementedException();
                        });
                });

            Assert.Single(times);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task FailDelayed()
        {
            var policy = new ExponentialRetryPolicy(TransientDetector);
            var times  = new List<DateTime>();

            await Assert.ThrowsAsync<NotImplementedException>(
                async () =>
                {
                    await policy.InvokeAsync(
                        async () =>
                        {
                            times.Add(DateTime.UtcNow);
                            await Task.CompletedTask;

                            if (times.Count < 2)
                            {
                                throw new TransientException();
                            }
                            else
                            {
                                throw new NotImplementedException();
                            }
                        });
                });

            Assert.Equal(2, times.Count);
            VerifyIntervals(times, policy);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task FailDelayed_Result()
        {
            var policy = new ExponentialRetryPolicy(TransientDetector);
            var times  = new List<DateTime>();

            await Assert.ThrowsAsync<NotImplementedException>(
                async () =>
                {
                    await policy.InvokeAsync<string>(
                        async () =>
                        {
                            times.Add(DateTime.UtcNow);
                            await Task.CompletedTask;

                            if (times.Count < 2)
                            {
                                throw new TransientException();
                            }
                            else
                            {
                                throw new NotImplementedException();
                            }
                        });
                });

            Assert.Equal(2, times.Count);
            VerifyIntervals(times, policy);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task SuccessImmediate()
        {
            var policy  = new ExponentialRetryPolicy(TransientDetector);
            var times   = new List<DateTime>();
            var success = false;

            await policy.InvokeAsync(
                async () =>
                {
                    times.Add(DateTime.UtcNow);
                    await Task.CompletedTask;

                    success = true;
                });

            Assert.Single(times);
            Assert.True(success);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task SuccessImmediate_Result()
        {
            var policy = new ExponentialRetryPolicy(TransientDetector);
            var times   = new List<DateTime>();

            var success = await policy.InvokeAsync(
                async () =>
                {
                    times.Add(DateTime.UtcNow);
                    await Task.CompletedTask;

                    return "WOOHOO!";
                });

            Assert.Single(times);
            Assert.Equal("WOOHOO!", success);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task SuccessDelayed()
        {
            var policy  = new ExponentialRetryPolicy(TransientDetector);
            var times   = new List<DateTime>();
            var success = false;

            await policy.InvokeAsync(
                async () =>
                {
                    times.Add(DateTime.UtcNow);
                    await Task.CompletedTask;

                    if (times.Count < policy.MaxAttempts)
                    {
                        throw new TransientException();
                    }

                    success = true;
                });

            Assert.True(success);
            Assert.Equal(policy.MaxAttempts, times.Count);
            VerifyIntervals(times, policy);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task SuccessDelayed_Result()
        {
            var policy  = new ExponentialRetryPolicy(TransientDetector);
            var times   = new List<DateTime>();

            var success = await policy.InvokeAsync(
                async () =>
                {
                    times.Add(DateTime.UtcNow);
                    await Task.CompletedTask;

                    if (times.Count < policy.MaxAttempts)
                    {
                        throw new TransientException();
                    }

                    return "WOOHOO!";
                });

            Assert.Equal("WOOHOO!", success);
            Assert.Equal(policy.MaxAttempts, times.Count);
            VerifyIntervals(times, policy);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task SuccessDelayedByType()
        {
            var policy = new ExponentialRetryPolicy(typeof(NotReadyException));
            var times = new List<DateTime>();
            var success = false;

            await policy.InvokeAsync(
                async () =>
                {
                    times.Add(DateTime.UtcNow);
                    await Task.CompletedTask;

                    if (times.Count < policy.MaxAttempts)
                    {
                        throw new NotReadyException();
                    }

                    success = true;
                });

            Assert.True(success);
            Assert.Equal(policy.MaxAttempts, times.Count);
            VerifyIntervals(times, policy);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task SuccessDelayedAggregateSingle()
        {
            var policy = new ExponentialRetryPolicy(typeof(NotReadyException));
            var times  = new List<DateTime>();
            var success = false;

            await policy.InvokeAsync(
                async () =>
                {
                    times.Add(DateTime.UtcNow);
                    await Task.CompletedTask;

                    if (times.Count < policy.MaxAttempts)
                    {
                        throw new AggregateException(new NotReadyException());
                    }

                    success = true;
                });

            Assert.True(success);
            Assert.Equal(policy.MaxAttempts, times.Count);
            VerifyIntervals(times, policy);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task SuccessDelayedAggregateArray()
        {
            var policy  = new ExponentialRetryPolicy(new Type[] { typeof(NotReadyException), typeof(KeyNotFoundException) });
            var times   = new List<DateTime>();
            var success = false;

            await policy.InvokeAsync(
                async () =>
                {
                    times.Add(DateTime.UtcNow);
                    await Task.CompletedTask;

                    if (times.Count < policy.MaxAttempts)
                    {
                        if (times.Count % 1 == 0)
                        {
                            throw new AggregateException(new NotReadyException());
                        }
                        else
                        {
                            throw new AggregateException(new KeyNotFoundException());
                        }
                    }

                    success = true;
                });

            Assert.True(success);
            Assert.Equal(policy.MaxAttempts, times.Count);
            VerifyIntervals(times, policy);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task SuccessCustom()
        {
            var policy  = new ExponentialRetryPolicy(TransientDetector, maxAttempts: 6, initialRetryInterval: TimeSpan.FromSeconds(0.5), maxRetryInterval: TimeSpan.FromSeconds(4));
            var times   = new List<DateTime>();
            var success = false;

            Assert.Equal(6, policy.MaxAttempts);
            Assert.Equal(TimeSpan.FromSeconds(0.5), policy.InitialRetryInterval);
            Assert.Equal(TimeSpan.FromSeconds(4), policy.MaxRetryInterval);

            await policy.InvokeAsync(
                async () =>
                {
                    times.Add(DateTime.UtcNow);
                    await Task.CompletedTask;

                    if (times.Count < policy.MaxAttempts)
                    {
                        throw new TransientException();
                    }

                    success = true;
                });

            Assert.True(success);
            Assert.Equal(policy.MaxAttempts, times.Count);
            VerifyIntervals(times, policy);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task SuccessCustom_Result()
        {
            var policy = new ExponentialRetryPolicy(TransientDetector, maxAttempts: 6, initialRetryInterval: TimeSpan.FromSeconds(0.5), maxRetryInterval: TimeSpan.FromSeconds(4));
            var times  = new List<DateTime>();

            Assert.Equal(6, policy.MaxAttempts);
            Assert.Equal(TimeSpan.FromSeconds(0.5), policy.InitialRetryInterval);
            Assert.Equal(TimeSpan.FromSeconds(4), policy.MaxRetryInterval);

            var success = await policy.InvokeAsync(
                async () =>
                {
                    times.Add(DateTime.UtcNow);
                    await Task.CompletedTask;

                    if (times.Count < policy.MaxAttempts)
                    {
                        throw new TransientException();
                    }

                    return "WOOHOO!";
                });

            Assert.Equal("WOOHOO!", success);
            Assert.Equal(policy.MaxAttempts, times.Count);
            VerifyIntervals(times, policy);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public async Task Timeout()
        {
            var policy = new ExponentialRetryPolicy(TransientDetector, maxAttempts: 6, initialRetryInterval: TimeSpan.FromSeconds(0.5), maxRetryInterval: TimeSpan.FromSeconds(4), timeout: TimeSpan.FromSeconds(1.5));
            var times  = new List<DateTime>();

            Assert.Equal(6, policy.MaxAttempts);
            Assert.Equal(TimeSpan.FromSeconds(0.5), policy.InitialRetryInterval);
            Assert.Equal(TimeSpan.FromSeconds(4), policy.MaxRetryInterval);
            Assert.Equal(TimeSpan.FromSeconds(1.5), policy.Timeout);

            await Assert.ThrowsAsync<TransientException>(
                async () =>
                {
                    await policy.InvokeAsync(
                        async () =>
                        {
                            times.Add(DateTime.UtcNow);
                            await Task.CompletedTask;

                            throw new TransientException();
                        });
                });

            Assert.True(times.Count < 6);
        }
    }
}

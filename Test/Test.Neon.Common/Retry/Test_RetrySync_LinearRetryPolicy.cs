//-----------------------------------------------------------------------------
// FILE:	    Test_RetrySync_LinearRetryPolicy.cs
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
using Neon.Retry;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    [Trait(TestTrait.Area, TestArea.NeonCommon)]
    public class Test_RetrySync_LinearRetryPolicy
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
        private void VerifyIntervals(List<DateTime> times, LinearRetryPolicy policy)
        {
            for (int i = 0; i < times.Count - 1; i++)
            {
                Assert.True(VerifyInterval(times[i], times[i + 1], policy.RetryInterval));
            }
        }

        [Fact]
        public void Defaults()
        {
            var policy = new LinearRetryPolicy(TransientDetector);

            Assert.Equal(5, policy.MaxAttempts);
            Assert.Equal(TimeSpan.FromSeconds(1), policy.RetryInterval);
        }

        [Fact]
        public void FailAll()
        {
            var policy = new LinearRetryPolicy(TransientDetector);
            var times  = new List<DateTime>();

            Assert.Throws<TransientException>(
                () =>
                {
                    policy.Invoke(
                        () =>
                        {
                            times.Add(DateTime.UtcNow);
                            throw new TransientException();
                        });
                });

            Assert.Equal(policy.MaxAttempts , times.Count);
            VerifyIntervals(times, policy);
        }

        [Fact]
        public void FailAll_Result()
        {
            var policy = new LinearRetryPolicy(TransientDetector);
            var times  = new List<DateTime>();

            Assert.Throws<TransientException>(
                () =>
                {
                    policy.Invoke<string>(
                        () =>
                        {
                            times.Add(DateTime.UtcNow);
                            throw new TransientException();
                        });
                });

            Assert.Equal(policy.MaxAttempts, times.Count);
            VerifyIntervals(times, policy);
        }

        [Fact]
        public void FailImmediate()
        {
            var policy = new LinearRetryPolicy(TransientDetector);
            var times  = new List<DateTime>();

            Assert.Throws<NotImplementedException>(
                () =>
                {
                    policy.Invoke(
                        () =>
                        {
                            times.Add(DateTime.UtcNow);
                            throw new NotImplementedException();
                        });
                });

            Assert.Single(times);
        }

        [Fact]
        public void FailImmediate_Result()
        {
            var policy = new LinearRetryPolicy(TransientDetector);
            var times  = new List<DateTime>();

            Assert.Throws<NotImplementedException>(
                () =>
                {
                    policy.Invoke<string>(
                        () =>
                        {
                            times.Add(DateTime.UtcNow);
                            throw new NotImplementedException();
                        });
                });

            Assert.Single(times);
        }

        [Fact]
        public void FailDelayed()
        {
            var policy = new LinearRetryPolicy(TransientDetector);
            var times  = new List<DateTime>();

            Assert.Throws<NotImplementedException>(
                () =>
                {
                    policy.Invoke(
                        () =>
                        {
                            times.Add(DateTime.UtcNow);

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
        public void FailDelayed_Result()
        {
            var policy = new LinearRetryPolicy(TransientDetector);
            var times  = new List<DateTime>();

            Assert.Throws<NotImplementedException>(
                () =>
                {
                    policy.Invoke<string>(
                        () =>
                        {
                            times.Add(DateTime.UtcNow);

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
        public void SuccessImmediate()
        {
            var policy  = new LinearRetryPolicy(TransientDetector);
            var times   = new List<DateTime>();
            var success = false;

            policy.Invoke(
                () =>
                {
                    times.Add(DateTime.UtcNow);

                    success = true;
                });

            Assert.Single(times);
            Assert.True(success);
        }

        [Fact]
        public void SuccessImmediate_Result()
        {
            var policy = new LinearRetryPolicy(TransientDetector);
            var times  = new List<DateTime>();

            var success = policy.Invoke(
                () =>
                {
                    times.Add(DateTime.UtcNow);

                    return "WOOHOO!";
                });

            Assert.Single(times);
            Assert.Equal("WOOHOO!", success);
        }

        [Fact]
        public void SuccessDelayed()
        {
            var policy  = new LinearRetryPolicy(TransientDetector);
            var times   = new List<DateTime>();
            var success = false;

            policy.Invoke(
                () =>
                {
                    times.Add(DateTime.UtcNow);

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
        public void SuccessDelayed_Result()
        {
            var policy = new LinearRetryPolicy(TransientDetector);
            var times  = new List<DateTime>();

            var success = policy.Invoke(
                () =>
                {
                    times.Add(DateTime.UtcNow);

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
        public void SuccessDelayedByType()
        {
            var policy  = new LinearRetryPolicy(typeof(NotReadyException));
            var times   = new List<DateTime>();
            var success = false;

            policy.Invoke(
                () =>
                {
                    times.Add(DateTime.UtcNow);

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
        public void SuccessDelayedAggregateSingle()
        {
            var policy  = new LinearRetryPolicy(typeof(NotReadyException));
            var times   = new List<DateTime>();
            var success = false;

            policy.Invoke(
                () =>
                {
                    times.Add(DateTime.UtcNow);

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
        public void SuccessDelayedAggregateArray()
        {
            var policy  = new LinearRetryPolicy(new Type[] { typeof(NotReadyException), typeof(KeyNotFoundException) });
            var times   = new List<DateTime>();
            var success = false;

            policy.Invoke(
                () =>
                {
                    times.Add(DateTime.UtcNow);

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
        public void SuccessCustom()
        {
            var policy  = new LinearRetryPolicy(TransientDetector, maxAttempts: 4, retryInterval: TimeSpan.FromSeconds(2));
            var times   = new List<DateTime>();
            var success = false;

            Assert.Equal(4, policy.MaxAttempts);
            Assert.Equal(TimeSpan.FromSeconds(2), policy.RetryInterval);

            policy.Invoke(
                () =>
                {
                    times.Add(DateTime.UtcNow);

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
        public void SuccessCustom_Result()
        {
            var policy = new LinearRetryPolicy(TransientDetector, maxAttempts: 4, retryInterval: TimeSpan.FromSeconds(2));
            var times  = new List<DateTime>();

            Assert.Equal(4, policy.MaxAttempts);
            Assert.Equal(TimeSpan.FromSeconds(2), policy.RetryInterval);

            var success = policy.Invoke(
                () =>
                {
                    times.Add(DateTime.UtcNow);

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
        public void Timeout()
        {
            var policy = new LinearRetryPolicy(TransientDetector, maxAttempts: 6, retryInterval: TimeSpan.FromSeconds(0.5), timeout: TimeSpan.FromSeconds(1.5));
            var times  = new List<DateTime>();

            Assert.Equal(6, policy.MaxAttempts);
            Assert.Equal(TimeSpan.FromSeconds(0.5), policy.RetryInterval);
            Assert.Equal(TimeSpan.FromSeconds(1.5), policy.Timeout);

            Assert.Throws<TransientException>(
                () =>
                {
                    policy.Invoke(
                        () =>
                        {
                            times.Add(DateTime.UtcNow);

                            throw new TransientException();
                        });
                });

            Assert.Equal(6, times.Count);

            // Additional test to verify this serious problem is fixed:
            //
            //      https://github.com/nforgeio/neonKUBE/issues/762
            //
            // We'll wait a bit longer to enure that any (incorrect) deadline computed
            // by the policy when constructed above does not impact a subsequent run.

            Thread.Sleep(TimeSpan.FromSeconds(4));

            times.Clear();

            Assert.Equal(6, policy.MaxAttempts);
            Assert.Equal(TimeSpan.FromSeconds(0.5), policy.RetryInterval);
            Assert.Equal(TimeSpan.FromSeconds(1.5), policy.Timeout);

            Assert.Throws<TransientException>(
                () =>
                {
                    policy.Invoke(
                        () =>
                        {
                            times.Add(DateTime.UtcNow);

                            throw new TransientException();
                        });
                });

            Assert.Equal(6, times.Count);
        }
    }
}

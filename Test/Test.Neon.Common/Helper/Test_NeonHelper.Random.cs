//-----------------------------------------------------------------------------
// FILE:	    Test_NeonHelper.Random.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public partial class Test_NeonHelper
    {
        [Fact]
        public void Base36Uuid_Pseudo()
        {
            // Generate 50K base-36 pseudo random UUIDs and verify that the're all
            // 13-characters, consist of lowercase letters and digits and also that we
            // never generate duplicates.

            var existing = new HashSet<string>();

            for (int i = 0; i < 50000; i++)
            {
                var v = NeonHelper.CreateBase36Uuid(secure: false);

                Assert.NotNull(v);
                Assert.Equal(13, v.Length);

                foreach (var ch in v)
                {
                    if (!char.IsLetterOrDigit(ch))
                    {
                        Assert.False(false, $"Invalid character [{ch}] found in [{v}].");
                    }
                    else if (char.IsUpper(ch))
                    {
                        Assert.False(false, $"Uppercase character [{ch}] found in [{v}].");
                    }
                    else if (ch > (char)127)
                    {
                        Assert.False(false, $"Non-ASCII character [{ch}] found in [{v}].");
                    }
                }

                Assert.DoesNotContain(v, existing);
                existing.Add(v);
            }
        }

        [Fact]
        public void Base36Uuid_Crypto()
        {
            // Generate 20K base-36 cyoptographically secure random UUIDs and verify that
            // the're all 13-characters, consist of lowercase letters and digits and also
            // that we never generate duplicates.

            var existing = new HashSet<string>();

            for (int i = 0; i < 20000; i++)
            {
                var v = NeonHelper.CreateBase36Uuid(secure: true);

                Assert.NotNull(v);
                Assert.Equal(13, v.Length);

                foreach (var ch in v)
                {
                    if (!char.IsLetterOrDigit(ch))
                    {
                        Assert.False(false, $"Invalid character [{ch}] found in [{v}].");
                    }
                    else if (char.IsUpper(ch))
                    {
                        Assert.False(false, $"Uppercase character [{ch}] found in [{v}].");
                    }
                    else if (ch > (char)127)
                    {
                        Assert.False(false, $"Non-ASCII character [{ch}] found in [{v}].");
                    }
                }

                Assert.DoesNotContain(v, existing);
                existing.Add(v);
            }
        }

        [Fact]
        public void PseudoRandomTimeSpan_Max()
        {
            Assert.Equal(TimeSpan.Zero, NeonHelper.PseudoRandomTimespan(TimeSpan.Zero));

            var max = TimeSpan.FromSeconds(10);

            for (int i = 0; i < 1000000; i++)
            {
                var value = NeonHelper.PseudoRandomTimespan(max);

                Assert.True(value >= TimeSpan.Zero);
                Assert.True(value <= max);
            }

            // Verify error detection.

            Assert.Throws<ArgumentException>(() => NeonHelper.PseudoRandomTimespan(TimeSpan.FromSeconds(-1)));
        }

        [Fact]
        public void PseudoRandomTimeSpan_Fraction()
        {
            Assert.Equal(TimeSpan.Zero, NeonHelper.PseudoRandomTimespan(TimeSpan.Zero, 0.0));
            Assert.Equal(TimeSpan.FromSeconds(10), NeonHelper.PseudoRandomTimespan(TimeSpan.FromSeconds(10), 0.0));

            var @base = TimeSpan.FromSeconds(10);
            var max   = TimeSpan.FromTicks((long)(@base.Ticks * 1.5));

            for (int i = 0; i < 1000000; i++)
            {
                var value = NeonHelper.PseudoRandomTimespan(@base, 0.5);

                Assert.True(value >= @base);
                Assert.True(value <= max);
            }

            // Verify error detection.

            Assert.Throws<ArgumentException>(() => NeonHelper.PseudoRandomTimespan(TimeSpan.FromSeconds(-1), 0.5));
            Assert.Throws<ArgumentException>(() => NeonHelper.PseudoRandomTimespan(TimeSpan.FromSeconds(10), -0.5));
        }

        [Fact]
        public void PseudoRandomTimeSpan_MinMax()
        {
            Assert.Equal(TimeSpan.Zero, NeonHelper.PseudoRandomTimespan(TimeSpan.Zero, TimeSpan.Zero));

            var min = TimeSpan.FromSeconds(5);
            var max = TimeSpan.FromSeconds(10);

            for (int i = 0; i < 1000000; i++)
            {
                var value = NeonHelper.PseudoRandomTimespan(min, max);

                Assert.True(value >= min);
                Assert.True(value <= max);
            }

            // Verify error detection.

            Assert.Throws<ArgumentException>(() => NeonHelper.PseudoRandomTimespan(TimeSpan.FromSeconds(-1), TimeSpan.FromSeconds(1)));
            Assert.Throws<ArgumentException>(() => NeonHelper.PseudoRandomTimespan(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(-1)));
            Assert.Throws<ArgumentException>(() => NeonHelper.PseudoRandomTimespan(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(5)));
        }
    }
}

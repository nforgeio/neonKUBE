//-----------------------------------------------------------------------------
// FILE:	    Test_ByteUnits.cs
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

// $todo(jeff.lill):
//
// PB and PiB units aren't working due to flowting point precision issues.
// I'm going to disable this for now.  Perhaps we can address this by using
// [decimal] instead of [double].

namespace TestCommon
{
    public class Test_ByteUnits
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ParseBase2()
        {
            // Verify that the units are correct.

            Assert.Equal(Math.Pow(2, 10), ByteUnits.KibiBytes);
            Assert.Equal(Math.Pow(2, 20), ByteUnits.MebiBytes);
            Assert.Equal(Math.Pow(2, 30), ByteUnits.GibiBytes);
            Assert.Equal(Math.Pow(2, 40), ByteUnits.TebiBytes);
#if ALLOW_PENTA
            Assert.Equal(Math.Pow(2, 50), ByteUnits.PebiBytes);
#endif

            long value;

            // Parse whole values.

            Assert.True(ByteUnits.TryParse("0", out value));
            Assert.Equal(0.0, value);

            Assert.True(ByteUnits.TryParse("4kib", out value));
            Assert.Equal(ByteUnits.KibiBytes * 4, value);

            Assert.True(ByteUnits.TryParse("4mib", out value));
            Assert.Equal(ByteUnits.MebiBytes * 4, value);

            Assert.True(ByteUnits.TryParse("7GiB", out value));
            Assert.Equal(ByteUnits.GibiBytes * 7, value);

            Assert.True(ByteUnits.TryParse("2TiB", out value));
            Assert.Equal(ByteUnits.TebiBytes * 2, value);

            Assert.True(ByteUnits.TryParse("2GiB", out value));
            Assert.Equal(ByteUnits.GibiBytes * 2, value);

            Assert.True(ByteUnits.TryParse("4tib", out value));
            Assert.Equal(ByteUnits.TebiBytes * 4, value);

#if ALLOW_PENTA
            Assert.True(ByteUnits.TryParse("3pib", out value));
            Assert.Equal(ByteUnits.PebiBytes * 3, value);
#endif

            // Test fractional values.

            Assert.True(ByteUnits.TryParse("1.5KiB", out value));
            Assert.Equal(ByteUnits.KibiBytes * 1.5, value);

            Assert.True(ByteUnits.TryParse("1.5MiB", out value));
            Assert.Equal(ByteUnits.MebiBytes * 1.5, value);

            Assert.True(ByteUnits.TryParse("1.5GiB", out value));
            Assert.Equal(ByteUnits.GibiBytes * 1.5, value);

            Assert.True(ByteUnits.TryParse("1.5TiB", out value));
            Assert.Equal(ByteUnits.TebiBytes * 1.5, value);

#if ALLOW_PENTA
            Assert.True(ByteUnits.TryParse("1.5PiB", out value));
            Assert.Equal(ByteUnits.PebiBytes * 1.5, value);
#endif

            // Parse values with a space before the units.

            Assert.True(ByteUnits.TryParse("1 KiB", out value));
            Assert.Equal(1.0 * ByteUnits.KibiBytes, value);

            Assert.True(ByteUnits.TryParse("2 MiB", out value));
            Assert.Equal(2.0 * ByteUnits.MebiBytes, value);

            Assert.True(ByteUnits.TryParse("3 GiB", out value));
            Assert.Equal(3.0 * ByteUnits.GibiBytes, value);

            Assert.True(ByteUnits.TryParse("4 TiB", out value));
            Assert.Equal(4.0 * ByteUnits.TebiBytes, value);

#if ALLOW_PENTA
            Assert.True(ByteUnits.TryParse("9 P", out value));
            Assert.Equal(9.0 * ByteUnits.PebiBytes, value);

            Assert.True(ByteUnits.TryParse("10 PB", out value));
            Assert.Equal(10.0 * ByteUnits.PebiBytes, value);
#endif
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ParseBase10()
        {
            // Verify that the units are correct.

            Assert.Equal(1000L, ByteUnits.KiloBytes);
            Assert.Equal(1000000L, ByteUnits.MegaBytes);
            Assert.Equal(1000000000L, ByteUnits.GigaBytes);
            Assert.Equal(1000000000000L, ByteUnits.TeraBytes);
#if ALLOW_PENTA
            Assert.Equal(1000000000000000L, ByteUnits.PentaBytes);
#endif

            long value;

            // Parse whole values.

            Assert.True(ByteUnits.TryParse("0", out value));
            Assert.Equal(0, value);

            Assert.True(ByteUnits.TryParse("10b", out value));
            Assert.Equal(10, value);

            Assert.True(ByteUnits.TryParse("20B", out value));
            Assert.Equal(20, value);

            Assert.True(ByteUnits.TryParse("1K", out value));
            Assert.Equal(ByteUnits.KiloBytes, value);

            Assert.True(ByteUnits.TryParse("2KB", out value));
            Assert.Equal(ByteUnits.KiloBytes * 2, value);

            Assert.True(ByteUnits.TryParse("3k", out value));
            Assert.Equal(ByteUnits.KiloBytes * 3, value);

            Assert.True(ByteUnits.TryParse("4kb", out value));
            Assert.Equal(ByteUnits.KiloBytes * 4, value);

            Assert.True(ByteUnits.TryParse("1M", out value));
            Assert.Equal(ByteUnits.MegaBytes, value);

            Assert.True(ByteUnits.TryParse("2MB", out value));
            Assert.Equal(ByteUnits.MegaBytes * 2, value);

            Assert.True(ByteUnits.TryParse("3m", out value));
            Assert.Equal(ByteUnits.MegaBytes * 3, value);

            Assert.True(ByteUnits.TryParse("4mb", out value));
            Assert.Equal(ByteUnits.MegaBytes * 4, value);

            Assert.True(ByteUnits.TryParse("1G", out value));
            Assert.Equal(ByteUnits.GigaBytes, value);

            Assert.True(ByteUnits.TryParse("2TB", out value));
            Assert.Equal(ByteUnits.TeraBytes * 2, value);

            Assert.True(ByteUnits.TryParse("1T", out value));
            Assert.Equal(ByteUnits.TeraBytes, value);

            Assert.True(ByteUnits.TryParse("2GB", out value));
            Assert.Equal(ByteUnits.GigaBytes * 2, value);

            Assert.True(ByteUnits.TryParse("3g", out value));
            Assert.Equal(ByteUnits.GigaBytes * 3, value);

            Assert.True(ByteUnits.TryParse("4gb", out value));
            Assert.Equal(ByteUnits.GigaBytes * 4, value);

            Assert.True(ByteUnits.TryParse("3t", out value));
            Assert.Equal(ByteUnits.TeraBytes * 3, value);

            Assert.True(ByteUnits.TryParse("4tb", out value));
            Assert.Equal(ByteUnits.TeraBytes * 4, value);

#if ALLOW_PENTA
            Assert.True(ByteUnits.TryParse("3p", out value));
            Assert.Equal(ByteUnits.PentaBytes * 3, value);

            Assert.True(ByteUnits.TryParse("4pb", out value));
            Assert.Equal(ByteUnits.PentaBytes * 4, value);
#endif

            // Parse fractional values.

            Assert.True(ByteUnits.TryParse("1.5KB", out value));
            Assert.Equal(ByteUnits.KiloBytes * 1.5, value);

            Assert.True(ByteUnits.TryParse("1.5MB", out value));
            Assert.Equal(ByteUnits.MegaBytes * 1.5, value);

            Assert.True(ByteUnits.TryParse("1.5GB", out value));
            Assert.Equal(ByteUnits.GigaBytes * 1.5, value);

            Assert.True(ByteUnits.TryParse("1.5TB", out value));
            Assert.Equal(ByteUnits.TeraBytes * 1.5, value);

#if ALLOW_PENTA
            Assert.True(ByteUnits.TryParse("1.5PB", out value));
            Assert.Equal(ByteUnits.PentaBytes * 1.5, value);
#endif

            // Parse values with a space before the units.

            Assert.True(ByteUnits.TryParse("1 B", out value));
            Assert.Equal(1, value);

            Assert.True(ByteUnits.TryParse("2 K", out value));
            Assert.Equal(2 * ByteUnits.KiloBytes, value);

            Assert.True(ByteUnits.TryParse("3 KB", out value));
            Assert.Equal(3 * ByteUnits.KiloBytes, value);

            Assert.True(ByteUnits.TryParse("4 M", out value));
            Assert.Equal(4 * ByteUnits.MegaBytes, value);

            Assert.True(ByteUnits.TryParse("5 MB", out value));
            Assert.Equal(5 * ByteUnits.MegaBytes, value);

            Assert.True(ByteUnits.TryParse("6 G", out value));
            Assert.Equal(6 * ByteUnits.GigaBytes, value);

            Assert.True(ByteUnits.TryParse("7 GB", out value));
            Assert.Equal(7 * ByteUnits.GigaBytes, value);

            Assert.True(ByteUnits.TryParse("8 T", out value));
            Assert.Equal(8 * ByteUnits.TeraBytes, value);

            Assert.True(ByteUnits.TryParse("9 TB", out value));
            Assert.Equal(9 * ByteUnits.TeraBytes, value);

#if ALLOW_PENTA
            Assert.True(ByteUnits.TryParse("9 P", out value));
            Assert.Equal(9.0 * ByteUnits.PebiBytes, value);

            Assert.True(ByteUnits.TryParse("10 PB", out value));
            Assert.Equal(10.0 * ByteUnits.PebiBytes, value);
#endif
        }

        [Fact]
        public void ParseErrors()
        {
            long value;

            Assert.False(ByteUnits.TryParse(null, out value));
            Assert.False(ByteUnits.TryParse("", out value));
            Assert.False(ByteUnits.TryParse("   ", out value));
            Assert.False(ByteUnits.TryParse("ABC", out value));
            Assert.False(ByteUnits.TryParse("-10", out value));
            Assert.False(ByteUnits.TryParse("-20KB", out value));
            Assert.False(ByteUnits.TryParse("10a", out value));
            Assert.False(ByteUnits.TryParse("10akb", out value));
        }

        [Fact]
        public void Strings()
        {
            Assert.Equal("500", ByteUnits.ToByteString(500));
            Assert.Equal("1000000", ByteUnits.ToByteString(1000000));

            Assert.Equal("1KB", ByteUnits.ToKBString(1000));
            Assert.Equal("2KB", ByteUnits.ToKBString(2000));
            Assert.Equal("0.5KB", ByteUnits.ToKBString(500));

            Assert.Equal("1KiB", ByteUnits.ToKiBString(1024));
            Assert.Equal("2KiB", ByteUnits.ToKiBString(2048));
            Assert.Equal("0.5KiB", ByteUnits.ToKiBString(512));

            Assert.Equal("1MB", ByteUnits.ToMBString(1000000));
            Assert.Equal("2MB", ByteUnits.ToMBString(2000000));
            Assert.Equal("0.5MB", ByteUnits.ToMBString(500000));

            Assert.Equal("1MiB", ByteUnits.ToMiBString(1 * ByteUnits.MebiBytes));
            Assert.Equal("2MiB", ByteUnits.ToMiBString(2 * ByteUnits.MebiBytes));
            Assert.Equal("0.5MiB", ByteUnits.ToMiBString(ByteUnits.MebiBytes/2));

            Assert.Equal("1GB", ByteUnits.ToGBString(1000000000));
            Assert.Equal("2GB", ByteUnits.ToGBString(2000000000));
            Assert.Equal("0.5GB", ByteUnits.ToGBString(500000000));

            Assert.Equal("1GiB", ByteUnits.ToGiBString(1 * ByteUnits.GibiBytes));
            Assert.Equal("2GiB", ByteUnits.ToGiBString(2 * ByteUnits.GibiBytes));
            Assert.Equal("0.5GiB", ByteUnits.ToGiBString(ByteUnits.GibiBytes/2));

            Assert.Equal("1TB", ByteUnits.ToTBString(1000000000000));
            Assert.Equal("2TB", ByteUnits.ToTBString(2000000000000));
            Assert.Equal("0.5TB", ByteUnits.ToTBString(500000000000));

            Assert.Equal("1TiB", ByteUnits.ToTiBString(1 * ByteUnits.TebiBytes));
            Assert.Equal("2TiB", ByteUnits.ToTiBString(2 * ByteUnits.TebiBytes));
            Assert.Equal("0.5TiB", ByteUnits.ToTiBString(ByteUnits.TebiBytes/2));

#if ALLOW_PENTA
            Assert.Equal("1PB", ByteUnits.ToPBString(1000000000000000));
            Assert.Equal("2PB", ByteUnits.ToPBString(2000000000000000));
            Assert.Equal("0.5PB", ByteUnits.ToPBString(500000000000000));

            Assert.Equal("1PiB", ByteUnits.ToPiBString(1 * ByteUnits.PebiBytes));
            Assert.Equal("2PiB", ByteUnits.ToPiBString(2 * ByteUnits.PebiBytes));
            Assert.Equal("0.5PiB", ByteUnits.ToPiBString(ByteUnits.PebiBytes/2));
#endif
        }
    }
}

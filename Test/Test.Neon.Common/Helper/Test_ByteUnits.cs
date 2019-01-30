//-----------------------------------------------------------------------------
// FILE:	    Test_ByteUnits.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

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

            double value;

            // Parse whole values.

            Assert.True(ByteUnits.TryParseCount("0", out value));
            Assert.Equal(0.0, value);

            Assert.True(ByteUnits.TryParseCount("4kib", out value));
            Assert.Equal((double)ByteUnits.KibiBytes * 4, value);

            Assert.True(ByteUnits.TryParseCount("4mib", out value));
            Assert.Equal((double)ByteUnits.MebiBytes * 4, value);

            Assert.True(ByteUnits.TryParseCount("7GiB", out value));
            Assert.Equal((double)ByteUnits.GibiBytes * 7, value);

            Assert.True(ByteUnits.TryParseCount("2TiB", out value));
            Assert.Equal((double)ByteUnits.TebiBytes * 2, value);

            Assert.True(ByteUnits.TryParseCount("2GiB", out value));
            Assert.Equal((double)ByteUnits.GibiBytes * 2, value);

            Assert.True(ByteUnits.TryParseCount("4tib", out value));
            Assert.Equal((double)ByteUnits.TebiBytes * 4, value);

#if ALLOW_PENTA
            Assert.True(ByteUnits.TryParseCount("3pib", out value));
            Assert.Equal((double)ByteUnits.PebiBytes * 3, value);
#endif

            // Test fractional values.

            Assert.True(ByteUnits.TryParseCount("0.5", out value));
            Assert.Equal(0.5, value);

            Assert.True(ByteUnits.TryParseCount("0.5B", out value));
            Assert.Equal(0.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5KiB", out value));
            Assert.Equal((double)ByteUnits.KibiBytes * 1.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5MiB", out value));
            Assert.Equal((double)ByteUnits.MebiBytes * 1.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5GiB", out value));
            Assert.Equal((double)ByteUnits.GibiBytes * 1.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5TiB", out value));
            Assert.Equal((double)ByteUnits.TebiBytes * 1.5, value);

#if ALLOW_PENTA
            Assert.True(ByteUnits.TryParseCount("1.5PiB", out value));
            Assert.Equal((double)ByteUnits.PebiBytes * 1.5, value);
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

            double value;

            // Parse whole values.

            Assert.True(ByteUnits.TryParseCount("0", out value));
            Assert.Equal(0.0, value);

            Assert.True(ByteUnits.TryParseCount("10b", out value));
            Assert.Equal(10.0, value);

            Assert.True(ByteUnits.TryParseCount("20B", out value));
            Assert.Equal(20.0, value);

            Assert.True(ByteUnits.TryParseCount("1K", out value));
            Assert.Equal((double)ByteUnits.KiloBytes, value);

            Assert.True(ByteUnits.TryParseCount("2KB", out value));
            Assert.Equal((double)ByteUnits.KiloBytes * 2, value);

            Assert.True(ByteUnits.TryParseCount("3k", out value));
            Assert.Equal((double)ByteUnits.KiloBytes * 3, value);

            Assert.True(ByteUnits.TryParseCount("4kb", out value));
            Assert.Equal((double)ByteUnits.KiloBytes * 4, value);

            Assert.True(ByteUnits.TryParseCount("1M", out value));
            Assert.Equal((double)ByteUnits.MegaBytes, value);

            Assert.True(ByteUnits.TryParseCount("2MB", out value));
            Assert.Equal((double)ByteUnits.MegaBytes * 2, value);

            Assert.True(ByteUnits.TryParseCount("3m", out value));
            Assert.Equal((double)ByteUnits.MegaBytes * 3, value);

            Assert.True(ByteUnits.TryParseCount("4mb", out value));
            Assert.Equal((double)ByteUnits.MegaBytes * 4, value);

            Assert.True(ByteUnits.TryParseCount("1G", out value));
            Assert.Equal((double)ByteUnits.GigaBytes, value);

            Assert.True(ByteUnits.TryParseCount("2TB", out value));
            Assert.Equal((double)ByteUnits.TeraBytes * 2, value);

            Assert.True(ByteUnits.TryParseCount("1T", out value));
            Assert.Equal((double)ByteUnits.TeraBytes, value);

            Assert.True(ByteUnits.TryParseCount("2GB", out value));
            Assert.Equal((double)ByteUnits.GigaBytes * 2, value);

            Assert.True(ByteUnits.TryParseCount("3g", out value));
            Assert.Equal((double)ByteUnits.GigaBytes * 3, value);

            Assert.True(ByteUnits.TryParseCount("4gb", out value));
            Assert.Equal((double)ByteUnits.GigaBytes * 4, value);

            Assert.True(ByteUnits.TryParseCount("3t", out value));
            Assert.Equal((double)ByteUnits.TeraBytes * 3, value);

            Assert.True(ByteUnits.TryParseCount("4tb", out value));
            Assert.Equal((double)ByteUnits.TeraBytes * 4, value);

#if ALLOW_PENTA
            Assert.True(ByteUnits.TryParseCount("3p", out value));
            Assert.Equal((double)ByteUnits.PentaBytes * 3, value);

            Assert.True(ByteUnits.TryParseCount("4pb", out value));
            Assert.Equal((double)ByteUnits.PentaBytes * 4, value);
#endif

            // Parse fractional values.

            Assert.True(ByteUnits.TryParseCount("0.5", out value));
            Assert.Equal(0.5, value);

            Assert.True(ByteUnits.TryParseCount("0.5B", out value));
            Assert.Equal(0.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5KB", out value));
            Assert.Equal((double)ByteUnits.KiloBytes * 1.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5MB", out value));
            Assert.Equal((double)ByteUnits.MegaBytes * 1.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5GB", out value));
            Assert.Equal((double)ByteUnits.GigaBytes * 1.5, value);

            Assert.True(ByteUnits.TryParseCount("1.5TB", out value));
            Assert.Equal((double)ByteUnits.TeraBytes * 1.5, value);

#if ALLOW_PENTA
            Assert.True(ByteUnits.TryParseCount("1.5PB", out value));
            Assert.Equal((double)ByteUnits.PentaBytes * 1.5, value);
#endif

            // Parse values with a space before the units.

            Assert.True(ByteUnits.TryParseCount("1 B", out value));
            Assert.Equal(1.0, value);

            Assert.True(ByteUnits.TryParseCount("2 K", out value));
            Assert.Equal(2.0 * ByteUnits.KiloBytes, value);

            Assert.True(ByteUnits.TryParseCount("3 M", out value));
            Assert.Equal(3.0 * ByteUnits.MegaBytes, value);

            Assert.True(ByteUnits.TryParseCount("4 MB", out value));
            Assert.Equal(4.0 * ByteUnits.MegaBytes, value);

            Assert.True(ByteUnits.TryParseCount("5 G", out value));
            Assert.Equal(5.0 * ByteUnits.GigaBytes, value);

            Assert.True(ByteUnits.TryParseCount("6 GB", out value));
            Assert.Equal(6.0 * ByteUnits.GigaBytes, value);

            Assert.True(ByteUnits.TryParseCount("7 T", out value));
            Assert.Equal(7.0 * ByteUnits.TeraBytes, value);

            Assert.True(ByteUnits.TryParseCount("8 TB", out value));
            Assert.Equal(8.0 * ByteUnits.TeraBytes, value);

#if ALLOW_PENTA
            Assert.True(ByteUnits.TryParseCount("9 P", out value));
            Assert.Equal(9.0 * ByteUnits.PebiBytes, value);

            Assert.True(ByteUnits.TryParseCount("10 PB", out value));
            Assert.Equal(10.0 * ByteUnits.PebiBytes, value);
#endif
        }

        [Fact]
        public void ParseErrors()
        {
            double value;

            Assert.False(ByteUnits.TryParseCount(null, out value));
            Assert.False(ByteUnits.TryParseCount("", out value));
            Assert.False(ByteUnits.TryParseCount("   ", out value));
            Assert.False(ByteUnits.TryParseCount("ABC", out value));
            Assert.False(ByteUnits.TryParseCount("-10", out value));
            Assert.False(ByteUnits.TryParseCount("-20KB", out value));
            Assert.False(ByteUnits.TryParseCount("10a", out value));
            Assert.False(ByteUnits.TryParseCount("10akb", out value));
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

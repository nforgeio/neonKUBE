//-----------------------------------------------------------------------------
// FILE:	    Test_ByteUnits.cs
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

// $todo(jefflill):
//
// PB and PiB units aren't working due to flowting point precision issues.
// I'm going to disable this for now.  Perhaps we can address this by using
// [decimal] instead of [double].

namespace TestCommon
{
    public class Test_ByteUnits
    {
        public static decimal Pow(decimal x, uint y)
        {
            decimal A = 1m;
            BitArray e = new BitArray(BitConverter.GetBytes(y));
            int t = e.Count;

            for (int i = t - 1; i >= 0; --i)
            {
                A *= A;
                if (e[i] == true)
                {
                    A *= x;
                }
            }
            return A;
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ParseBase2()
        {
            // Verify that the units are correct.

            Assert.Equal(Pow(2m, 10), ByteUnits.KibiBytes);
            Assert.Equal(Pow(2m, 20), ByteUnits.MebiBytes);
            Assert.Equal(Pow(2m, 30), ByteUnits.GibiBytes);
            Assert.Equal(Pow(2m, 40), ByteUnits.TebiBytes);
            Assert.Equal(Pow(2m, 50), ByteUnits.PebiBytes);
            Assert.Equal(Pow(2m, 60), ByteUnits.ExbiBytes);

            decimal value;

            // Parse whole values.

            Assert.True(ByteUnits.TryParse("1m", out value));
            Assert.Equal(0.001m, value);

            Assert.True(ByteUnits.TryParse("0", out value));
            Assert.Equal(0.0m, value);

            Assert.True(ByteUnits.TryParse("4Ki", out value));
            Assert.Equal(ByteUnits.KibiBytes * 4, value);

            Assert.True(ByteUnits.TryParse("4Mi", out value));
            Assert.Equal(ByteUnits.MebiBytes * 4, value);

            Assert.True(ByteUnits.TryParse("7Gi", out value));
            Assert.Equal(ByteUnits.GibiBytes * 7, value);

            Assert.True(ByteUnits.TryParse("2Ti", out value));
            Assert.Equal(ByteUnits.TebiBytes * 2, value);

            Assert.True(ByteUnits.TryParse("2Gi", out value));
            Assert.Equal(ByteUnits.GibiBytes * 2, value);

            Assert.True(ByteUnits.TryParse("4Ti", out value));
            Assert.Equal(ByteUnits.TebiBytes * 4, value);

            Assert.True(ByteUnits.TryParse("3Pi", out value));
            Assert.Equal(ByteUnits.PebiBytes * 3, value);

            Assert.True(ByteUnits.TryParse("5Ei", out value));
            Assert.Equal(ByteUnits.ExbiBytes * 5, value);

            // Test fractional values.

            Assert.True(ByteUnits.TryParse("1.5m", out value));
            Assert.Equal(0.001m * 1.5m, value);

            Assert.True(ByteUnits.TryParse("1.5", out value));
            Assert.Equal(1 * 1.5m, value);

            Assert.True(ByteUnits.TryParse("1.5Ki", out value));
            Assert.Equal(ByteUnits.KibiBytes * 1.5m, value);

            Assert.True(ByteUnits.TryParse("1.5Mi", out value));
            Assert.Equal(ByteUnits.MebiBytes * 1.5m, value);

            Assert.True(ByteUnits.TryParse("1.5Gi", out value));
            Assert.Equal(ByteUnits.GibiBytes * 1.5m, value);

            Assert.True(ByteUnits.TryParse("1.5Ti", out value));
            Assert.Equal(ByteUnits.TebiBytes * 1.5m, value);

            Assert.True(ByteUnits.TryParse("1.5Pi", out value));
            Assert.Equal(ByteUnits.PebiBytes * 1.5m, value);

            Assert.True(ByteUnits.TryParse("1.5Ei", out value));
            Assert.Equal(ByteUnits.ExbiBytes * 1.5m, value);

            // Parse values with a space before the units.

            Assert.True(ByteUnits.TryParse("1 m", out value));
            Assert.Equal(1.0m * 0.001m, value);

            Assert.True(ByteUnits.TryParse("1 Ki", out value));
            Assert.Equal(1.0m * ByteUnits.KibiBytes, value);

            Assert.True(ByteUnits.TryParse("2 Mi", out value));
            Assert.Equal(2.0m * ByteUnits.MebiBytes, value);

            Assert.True(ByteUnits.TryParse("3 Gi", out value));
            Assert.Equal(3.0m * ByteUnits.GibiBytes, value);

            Assert.True(ByteUnits.TryParse("4 Ti", out value));
            Assert.Equal(4.0m * ByteUnits.TebiBytes, value);

            Assert.True(ByteUnits.TryParse("9 Pi", out value));
            Assert.Equal(9.0m * ByteUnits.PebiBytes, value);

            Assert.True(ByteUnits.TryParse("10 Ei", out value));
            Assert.Equal(10.0m * ByteUnits.ExbiBytes, value);
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ParseBase10()
        {
            // Verify that the units are correct.

            Assert.Equal(1000m, ByteUnits.KiloBytes);
            Assert.Equal(1000000m, ByteUnits.MegaBytes);
            Assert.Equal(1000000000m, ByteUnits.GigaBytes);
            Assert.Equal(1000000000000m, ByteUnits.TeraBytes);
            Assert.Equal(1000000000000000m, ByteUnits.PetaBytes);
            Assert.Equal(1000000000000000000m, ByteUnits.ExaBytes);

            decimal value;

            // Parse whole values.
            Assert.True(ByteUnits.TryParse("300m", out value));
            Assert.Equal(0.3m, value);

            Assert.True(ByteUnits.TryParse("4000m", out value));
            Assert.Equal(4m, value);

            Assert.True(ByteUnits.TryParse("0", out value));
            Assert.Equal(0, value);

            Assert.True(ByteUnits.TryParse("10", out value));
            Assert.Equal(10, value);

            Assert.True(ByteUnits.TryParse("20", out value));
            Assert.Equal(20, value);

            Assert.True(ByteUnits.TryParse("1K", out value));
            Assert.Equal(ByteUnits.KiloBytes, value);

            Assert.True(ByteUnits.TryParse("2K", out value));
            Assert.Equal(ByteUnits.KiloBytes * 2, value);

            Assert.True(ByteUnits.TryParse("1M", out value));
            Assert.Equal(ByteUnits.MegaBytes, value);

            Assert.True(ByteUnits.TryParse("2M", out value));
            Assert.Equal(ByteUnits.MegaBytes * 2, value);

            Assert.True(ByteUnits.TryParse("1G", out value));
            Assert.Equal(ByteUnits.GigaBytes, value);

            Assert.True(ByteUnits.TryParse("2G", out value));
            Assert.Equal(ByteUnits.GigaBytes * 2, value);

            Assert.True(ByteUnits.TryParse("2T", out value));
            Assert.Equal(ByteUnits.TeraBytes * 2, value);

            Assert.True(ByteUnits.TryParse("1T", out value));
            Assert.Equal(ByteUnits.TeraBytes, value);

            Assert.True(ByteUnits.TryParse("2P", out value));
            Assert.Equal(ByteUnits.PetaBytes * 2, value);

            Assert.True(ByteUnits.TryParse("1P", out value));
            Assert.Equal(ByteUnits.PetaBytes, value);

            Assert.True(ByteUnits.TryParse("2E", out value));
            Assert.Equal(ByteUnits.ExaBytes * 2, value);

            Assert.True(ByteUnits.TryParse("1E", out value));
            Assert.Equal(ByteUnits.ExaBytes, value);

            // Parse fractional values.

            Assert.True(ByteUnits.TryParse("1.5K", out value));
            Assert.Equal(ByteUnits.KiloBytes * 1.5m, value);

            Assert.True(ByteUnits.TryParse("1.5M", out value));
            Assert.Equal(ByteUnits.MegaBytes * 1.5m, value);

            Assert.True(ByteUnits.TryParse("1.5G", out value));
            Assert.Equal(ByteUnits.GigaBytes * 1.5m, value);

            Assert.True(ByteUnits.TryParse("1.5T", out value));
            Assert.Equal(ByteUnits.TeraBytes * 1.5m, value);

            Assert.True(ByteUnits.TryParse("1.5P", out value));
            Assert.Equal(ByteUnits.PetaBytes * 1.5m, value);

            // Parse values with a space before the units.

            Assert.True(ByteUnits.TryParse("1 m", out value));
            Assert.Equal(0.001m, value);

            Assert.True(ByteUnits.TryParse("1 ", out value));
            Assert.Equal(1, value);

            Assert.True(ByteUnits.TryParse("2 K", out value));
            Assert.Equal(2 * ByteUnits.KiloBytes, value);

            Assert.True(ByteUnits.TryParse("3 K", out value));
            Assert.Equal(3 * ByteUnits.KiloBytes, value);

            Assert.True(ByteUnits.TryParse("4 M", out value));
            Assert.Equal(4 * ByteUnits.MegaBytes, value);

            Assert.True(ByteUnits.TryParse("5 M", out value));
            Assert.Equal(5 * ByteUnits.MegaBytes, value);

            Assert.True(ByteUnits.TryParse("6 G", out value));
            Assert.Equal(6 * ByteUnits.GigaBytes, value);

            Assert.True(ByteUnits.TryParse("7 G", out value));
            Assert.Equal(7 * ByteUnits.GigaBytes, value);

            Assert.True(ByteUnits.TryParse("8 T", out value));
            Assert.Equal(8 * ByteUnits.TeraBytes, value);

            Assert.True(ByteUnits.TryParse("9 T", out value));
            Assert.Equal(9 * ByteUnits.TeraBytes, value);

            Assert.True(ByteUnits.TryParse("9 P", out value));
            Assert.Equal(9 * ByteUnits.PetaBytes, value);

            Assert.True(ByteUnits.TryParse("10 P", out value));
            Assert.Equal(10 * ByteUnits.PetaBytes, value);
        }

        [Fact]
        public void ParseErrors()
        {
            decimal value;

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
            Assert.Equal("500m", ByteUnits.ToMilliByteString(0.5m));
            Assert.Equal("1000000m", ByteUnits.ToMilliByteString(1000));

            Assert.Equal("500", ByteUnits.ToByteString(500));
            Assert.Equal("1000000", ByteUnits.ToByteString(1000000));

            Assert.Equal("1K", ByteUnits.ToKString(1000));
            Assert.Equal("2K", ByteUnits.ToKString(2000));
            Assert.Equal("0.5K", ByteUnits.ToKString(500));

            Assert.Equal("1Ki", ByteUnits.ToKiString(1024));
            Assert.Equal("2Ki", ByteUnits.ToKiString(2048));
            Assert.Equal("0.5Ki", ByteUnits.ToKiString(512));

            Assert.Equal("1M", ByteUnits.ToMString(1000000));
            Assert.Equal("2M", ByteUnits.ToMString(2000000));
            Assert.Equal("0.5M", ByteUnits.ToMString(500000));

            Assert.Equal("1Mi", ByteUnits.ToMiString(1 * ByteUnits.MebiBytes));
            Assert.Equal("2Mi", ByteUnits.ToMiString(2 * ByteUnits.MebiBytes));
            Assert.Equal("0.5Mi", ByteUnits.ToMiString(ByteUnits.MebiBytes/2));

            Assert.Equal("1G", ByteUnits.ToGString(1000000000));
            Assert.Equal("2G", ByteUnits.ToGString(2000000000));
            Assert.Equal("0.5G", ByteUnits.ToGString(500000000));

            Assert.Equal("1Gi", ByteUnits.ToGiString(1 * ByteUnits.GibiBytes));
            Assert.Equal("2Gi", ByteUnits.ToGiString(2 * ByteUnits.GibiBytes));
            Assert.Equal("0.5Gi", ByteUnits.ToGiString(ByteUnits.GibiBytes/2));

            Assert.Equal("1T", ByteUnits.ToTString(1000000000000));
            Assert.Equal("2T", ByteUnits.ToTString(2000000000000));
            Assert.Equal("0.5T", ByteUnits.ToTString(500000000000));

            Assert.Equal("1Ti", ByteUnits.ToTiString(1 * ByteUnits.TebiBytes));
            Assert.Equal("2Ti", ByteUnits.ToTiString(2 * ByteUnits.TebiBytes));
            Assert.Equal("0.5Ti", ByteUnits.ToTiString(ByteUnits.TebiBytes/2));

            Assert.Equal("1P", ByteUnits.ToPString(1000000000000000));
            Assert.Equal("2P", ByteUnits.ToPString(2000000000000000));
            Assert.Equal("0.5P", ByteUnits.ToPString(500000000000000));

            Assert.Equal("1Pi", ByteUnits.ToPiString(1 * ByteUnits.PebiBytes));
            Assert.Equal("2Pi", ByteUnits.ToPiString(2 * ByteUnits.PebiBytes));
            Assert.Equal("0.5Pi", ByteUnits.ToPiString(ByteUnits.PebiBytes / 2));

            Assert.Equal("1E", ByteUnits.ToEString(1000000000000000000));
            Assert.Equal("2E", ByteUnits.ToEString(2000000000000000000));
            Assert.Equal("0.5E", ByteUnits.ToEString(500000000000000000));

            Assert.Equal("1Ei", ByteUnits.ToEiString(1 * ByteUnits.ExbiBytes));
            Assert.Equal("2Ei", ByteUnits.ToEiString(2 * ByteUnits.ExbiBytes));
            Assert.Equal("0.5Ei", ByteUnits.ToEiString(ByteUnits.ExbiBytes / 2));
        }
    }
}

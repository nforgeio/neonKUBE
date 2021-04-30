//-----------------------------------------------------------------------------
// FILE:	    Test_ByteUnits.cs
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
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
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
        [Trait(TestTrait.Project, TestProject.NeonCommon)]
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
            Assert.Equal("500", ByteUnits.ToByteString(500));
            Assert.Equal("1000000", ByteUnits.ToByteString(1000000));

            Assert.Equal("1KB", ByteUnits.ToKB(1000));
            Assert.Equal("2KB", ByteUnits.ToKB(2000));
            Assert.Equal("0.5KB", ByteUnits.ToKB(500));

            Assert.Equal("1KiB", ByteUnits.ToKiB(1024));
            Assert.Equal("2KiB", ByteUnits.ToKiB(2048));
            Assert.Equal("0.5KiB", ByteUnits.ToKiB(512));

            Assert.Equal("1MB", ByteUnits.ToMB(1000000));
            Assert.Equal("2MB", ByteUnits.ToMB(2000000));
            Assert.Equal("0.5MB", ByteUnits.ToMB(500000));

            Assert.Equal("1MiB", ByteUnits.ToMiB(1 * ByteUnits.MebiBytes));
            Assert.Equal("2MiB", ByteUnits.ToMiB(2 * ByteUnits.MebiBytes));
            Assert.Equal("0.5MiB", ByteUnits.ToMiB(ByteUnits.MebiBytes/2));

            Assert.Equal("1GB", ByteUnits.ToGB(1000000000));
            Assert.Equal("2GB", ByteUnits.ToGB(2000000000));
            Assert.Equal("0.5GB", ByteUnits.ToGB(500000000));

            Assert.Equal("1GiB", ByteUnits.ToGiB(1 * ByteUnits.GibiBytes));
            Assert.Equal("2GiB", ByteUnits.ToGiB(2 * ByteUnits.GibiBytes));
            Assert.Equal("0.5GiB", ByteUnits.ToGiB(ByteUnits.GibiBytes/2));

            Assert.Equal("1TB", ByteUnits.ToTB(1000000000000));
            Assert.Equal("2TB", ByteUnits.ToTB(2000000000000));
            Assert.Equal("0.5TB", ByteUnits.ToTB(500000000000));

            Assert.Equal("1TiB", ByteUnits.ToTiB(1 * ByteUnits.TebiBytes));
            Assert.Equal("2TiB", ByteUnits.ToTiB(2 * ByteUnits.TebiBytes));
            Assert.Equal("0.5TiB", ByteUnits.ToTiB(ByteUnits.TebiBytes/2));

            Assert.Equal("1PB", ByteUnits.ToPB(1000000000000000));
            Assert.Equal("2PB", ByteUnits.ToPB(2000000000000000));
            Assert.Equal("0.5PB", ByteUnits.ToPB(500000000000000));

            Assert.Equal("1PiB", ByteUnits.ToPiB(1 * ByteUnits.PebiBytes));
            Assert.Equal("2PiB", ByteUnits.ToPiB(2 * ByteUnits.PebiBytes));
            Assert.Equal("0.5PiB", ByteUnits.ToPiB(ByteUnits.PebiBytes / 2));

            Assert.Equal("1EB", ByteUnits.ToEB(1000000000000000000));
            Assert.Equal("2EB", ByteUnits.ToEB(2000000000000000000));
            Assert.Equal("0.5EB", ByteUnits.ToEB(500000000000000000));

            Assert.Equal("1EiB", ByteUnits.ToEiB(1 * ByteUnits.ExbiBytes));
            Assert.Equal("2EiB", ByteUnits.ToEiB(2 * ByteUnits.ExbiBytes));
            Assert.Equal("0.5EiB", ByteUnits.ToEiB(ByteUnits.ExbiBytes / 2));
        }

        [Fact]
        public void CaseInsensitive()
        {
            decimal value;

            Assert.True(ByteUnits.TryParse("1k", out value));
            Assert.Equal(ByteUnits.KiloBytes, value);

            Assert.True(ByteUnits.TryParse("1kb", out value));
            Assert.Equal(ByteUnits.KiloBytes, value);

            Assert.True(ByteUnits.TryParse("2ki", out value));
            Assert.Equal(ByteUnits.KibiBytes * 2, value);

            Assert.True(ByteUnits.TryParse("2kib", out value));
            Assert.Equal(ByteUnits.KibiBytes * 2, value);

            Assert.True(ByteUnits.TryParse("1m", out value));
            Assert.Equal(ByteUnits.MegaBytes, value);

            Assert.True(ByteUnits.TryParse("1mb", out value));
            Assert.Equal(ByteUnits.MegaBytes, value);

            Assert.True(ByteUnits.TryParse("2mi", out value));
            Assert.Equal(ByteUnits.MebiBytes * 2, value);

            Assert.True(ByteUnits.TryParse("2mib", out value));
            Assert.Equal(ByteUnits.MebiBytes * 2, value);

            Assert.True(ByteUnits.TryParse("1g", out value));
            Assert.Equal(ByteUnits.GigaBytes, value);

            Assert.True(ByteUnits.TryParse("1gb", out value));
            Assert.Equal(ByteUnits.GigaBytes, value);

            Assert.True(ByteUnits.TryParse("2gi", out value));
            Assert.Equal(ByteUnits.GibiBytes * 2, value);

            Assert.True(ByteUnits.TryParse("2gib", out value));
            Assert.Equal(ByteUnits.GibiBytes * 2, value);

            Assert.True(ByteUnits.TryParse("1t", out value));
            Assert.Equal(ByteUnits.TeraBytes, value);

            Assert.True(ByteUnits.TryParse("1tb", out value));
            Assert.Equal(ByteUnits.TeraBytes, value);

            Assert.True(ByteUnits.TryParse("2ti", out value));
            Assert.Equal(ByteUnits.TebiBytes * 2, value);

            Assert.True(ByteUnits.TryParse("2tib", out value));
            Assert.Equal(ByteUnits.TebiBytes * 2, value);

            Assert.True(ByteUnits.TryParse("1p", out value));
            Assert.Equal(ByteUnits.PetaBytes, value);

            Assert.True(ByteUnits.TryParse("1pb", out value));
            Assert.Equal(ByteUnits.PetaBytes, value);

            Assert.True(ByteUnits.TryParse("2pi", out value));
            Assert.Equal(ByteUnits.PebiBytes * 2, value);

            Assert.True(ByteUnits.TryParse("2pib", out value));
            Assert.Equal(ByteUnits.PebiBytes * 2, value);

            Assert.True(ByteUnits.TryParse("1e", out value));
            Assert.Equal(ByteUnits.ExaBytes, value);

            Assert.True(ByteUnits.TryParse("1eb", out value));
            Assert.Equal(ByteUnits.ExaBytes, value);

            Assert.True(ByteUnits.TryParse("2ei", out value));
            Assert.Equal(ByteUnits.ExbiBytes * 2, value);

            Assert.True(ByteUnits.TryParse("2eib", out value));
            Assert.Equal(ByteUnits.ExbiBytes * 2, value);
        }
    }
}

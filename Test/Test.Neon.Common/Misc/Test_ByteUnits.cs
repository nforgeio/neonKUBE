//-----------------------------------------------------------------------------
// FILE:	    Test_ByteUnits.cs
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
    [Trait(TestTrait.Category, TestArea.NeonCommon)]
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

        [Fact]
        public void Humanize_PowerOfTwo_WithSpace()
        {
            Assert.Equal("0", ByteUnits.Humanize(0, powerOfTwo: true));
            Assert.Equal("500", ByteUnits.Humanize(500, powerOfTwo: true));
            Assert.Equal("1000", ByteUnits.Humanize(1000, powerOfTwo: true));
            Assert.Equal("1 KiB", ByteUnits.Humanize(ByteUnits.KibiBytes, powerOfTwo: true));
            Assert.Equal("1.5 KiB", ByteUnits.Humanize(ByteUnits.KibiBytes + ByteUnits.KibiBytes / 2, powerOfTwo: true));
            Assert.Equal("1 MiB", ByteUnits.Humanize(ByteUnits.MebiBytes, powerOfTwo: true));
            Assert.Equal("1.5 MiB", ByteUnits.Humanize(ByteUnits.MebiBytes + ByteUnits.MebiBytes / 2, powerOfTwo: true));
            Assert.Equal("1 GiB", ByteUnits.Humanize(ByteUnits.GibiBytes, powerOfTwo: true));
            Assert.Equal("1.5 GiB", ByteUnits.Humanize(ByteUnits.GibiBytes + ByteUnits.GibiBytes / 2, powerOfTwo: true));
            Assert.Equal("1 TiB", ByteUnits.Humanize(ByteUnits.TebiBytes, powerOfTwo: true));
            Assert.Equal("1.5 TiB", ByteUnits.Humanize(ByteUnits.TebiBytes + ByteUnits.TebiBytes / 2, powerOfTwo: true));
            Assert.Equal("1 PiB", ByteUnits.Humanize(ByteUnits.PebiBytes, powerOfTwo: true));
            Assert.Equal("1.5 PiB", ByteUnits.Humanize(ByteUnits.PebiBytes + ByteUnits.PebiBytes / 2, powerOfTwo: true));
            Assert.Equal("1 EiB", ByteUnits.Humanize(ByteUnits.ExbiBytes, powerOfTwo: true));
            Assert.Equal("1.5 EiB", ByteUnits.Humanize(ByteUnits.ExbiBytes + ByteUnits.ExbiBytes / 2, powerOfTwo: true));

            // Verify that negative numbers are not supported.

            Assert.Throws<ArgumentException>(() => ByteUnits.Humanize(-1, powerOfTwo: true));
        }

        [Fact]
        public void Humanize_PowerOfTwo_WithoutSpace()
        {
            Assert.Equal("0", ByteUnits.Humanize(0, powerOfTwo: true, spaceBeforeUnit: false));
            Assert.Equal("500", ByteUnits.Humanize(500, powerOfTwo: true, spaceBeforeUnit: false));
            Assert.Equal("1000", ByteUnits.Humanize(1000, powerOfTwo: true, spaceBeforeUnit: false));
            Assert.Equal("1KiB", ByteUnits.Humanize(ByteUnits.KibiBytes, powerOfTwo: true, spaceBeforeUnit: false));
            Assert.Equal("1.5KiB", ByteUnits.Humanize(ByteUnits.KibiBytes + ByteUnits.KibiBytes / 2, powerOfTwo: true, spaceBeforeUnit: false));
            Assert.Equal("1MiB", ByteUnits.Humanize(ByteUnits.MebiBytes, powerOfTwo: true, spaceBeforeUnit: false));
            Assert.Equal("1.5MiB", ByteUnits.Humanize(ByteUnits.MebiBytes + ByteUnits.MebiBytes / 2, powerOfTwo: true, spaceBeforeUnit: false));
            Assert.Equal("1GiB", ByteUnits.Humanize(ByteUnits.GibiBytes, powerOfTwo: true, spaceBeforeUnit: false));
            Assert.Equal("1.5GiB", ByteUnits.Humanize(ByteUnits.GibiBytes + ByteUnits.GibiBytes / 2, powerOfTwo: true, spaceBeforeUnit: false));
            Assert.Equal("1TiB", ByteUnits.Humanize(ByteUnits.TebiBytes, powerOfTwo: true, spaceBeforeUnit: false));
            Assert.Equal("1.5TiB", ByteUnits.Humanize(ByteUnits.TebiBytes + ByteUnits.TebiBytes / 2, powerOfTwo: true, spaceBeforeUnit: false));
            Assert.Equal("1PiB", ByteUnits.Humanize(ByteUnits.PebiBytes, powerOfTwo: true, spaceBeforeUnit: false));
            Assert.Equal("1.5PiB", ByteUnits.Humanize(ByteUnits.PebiBytes + ByteUnits.PebiBytes / 2, powerOfTwo: true, spaceBeforeUnit: false));
            Assert.Equal("1EiB", ByteUnits.Humanize(ByteUnits.ExbiBytes, powerOfTwo: true, spaceBeforeUnit: false));
            Assert.Equal("1.5EiB", ByteUnits.Humanize(ByteUnits.ExbiBytes + ByteUnits.ExbiBytes / 2, powerOfTwo: true, spaceBeforeUnit: false));

            // Verify that negative numbers are not supported.

            Assert.Throws<ArgumentException>(() => ByteUnits.Humanize(-1, powerOfTwo: true, spaceBeforeUnit: false));
        }

        [Fact]
        public void Humanize_PowerOfTwo_NoB_WithSpace()
        {
            Assert.Equal("0", ByteUnits.Humanize(0, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("500", ByteUnits.Humanize(500, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1000", ByteUnits.Humanize(1000, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1 Ki", ByteUnits.Humanize(ByteUnits.KibiBytes, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1.5 Ki", ByteUnits.Humanize(ByteUnits.KibiBytes + ByteUnits.KibiBytes / 2, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1 Mi", ByteUnits.Humanize(ByteUnits.MebiBytes, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1.5 Mi", ByteUnits.Humanize(ByteUnits.MebiBytes + ByteUnits.MebiBytes / 2, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1 Gi", ByteUnits.Humanize(ByteUnits.GibiBytes, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1.5 Gi", ByteUnits.Humanize(ByteUnits.GibiBytes + ByteUnits.GibiBytes / 2, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1 Ti", ByteUnits.Humanize(ByteUnits.TebiBytes, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1.5 Ti", ByteUnits.Humanize(ByteUnits.TebiBytes + ByteUnits.TebiBytes / 2, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1 Pi", ByteUnits.Humanize(ByteUnits.PebiBytes, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1.5 Pi", ByteUnits.Humanize(ByteUnits.PebiBytes + ByteUnits.PebiBytes / 2, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1 Ei", ByteUnits.Humanize(ByteUnits.ExbiBytes, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1.5 Ei", ByteUnits.Humanize(ByteUnits.ExbiBytes + ByteUnits.ExbiBytes / 2, powerOfTwo: true, removeByteUnit: true));

            // Verify that negative numbers are not supported.

            Assert.Throws<ArgumentException>(() => ByteUnits.Humanize(-1, powerOfTwo: true));
        }

        [Fact]
        public void Humanize_PowerOfTwo_WithSpace_NoB()
        {
            Assert.Equal("0", ByteUnits.Humanize(0, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("500", ByteUnits.Humanize(500, powerOfTwo: true));
            Assert.Equal("1000", ByteUnits.Humanize(1000, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1 Ki", ByteUnits.Humanize(ByteUnits.KibiBytes, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1.5 Ki", ByteUnits.Humanize(ByteUnits.KibiBytes + ByteUnits.KibiBytes / 2, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1 Mi", ByteUnits.Humanize(ByteUnits.MebiBytes, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1.5 Mi", ByteUnits.Humanize(ByteUnits.MebiBytes + ByteUnits.MebiBytes / 2, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1 Gi", ByteUnits.Humanize(ByteUnits.GibiBytes, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1.5 Gi", ByteUnits.Humanize(ByteUnits.GibiBytes + ByteUnits.GibiBytes / 2, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1 Ti", ByteUnits.Humanize(ByteUnits.TebiBytes, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1.5 Ti", ByteUnits.Humanize(ByteUnits.TebiBytes + ByteUnits.TebiBytes / 2, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1 Pi", ByteUnits.Humanize(ByteUnits.PebiBytes, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1.5 Pi", ByteUnits.Humanize(ByteUnits.PebiBytes + ByteUnits.PebiBytes / 2, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1 Ei", ByteUnits.Humanize(ByteUnits.ExbiBytes, powerOfTwo: true, removeByteUnit: true));
            Assert.Equal("1.5 Ei", ByteUnits.Humanize(ByteUnits.ExbiBytes + ByteUnits.ExbiBytes / 2, powerOfTwo: true, removeByteUnit: true));

            // Verify that negative numbers are not supported.

            Assert.Throws<ArgumentException>(() => ByteUnits.Humanize(-1, powerOfTwo: true, removeByteUnit: true));
        }

        [Fact]
        public void Humanize_PowerOfTen_WithSpace()
        {
            Assert.Equal("0", ByteUnits.Humanize(0));
            Assert.Equal("500", ByteUnits.Humanize(500));
            Assert.Equal("1 KB", ByteUnits.Humanize(ByteUnits.KiloBytes));
            Assert.Equal("1.5 KB", ByteUnits.Humanize(ByteUnits.KiloBytes + ByteUnits.KiloBytes / 2));
            Assert.Equal("1 MB", ByteUnits.Humanize(ByteUnits.MegaBytes));
            Assert.Equal("1.5 MB", ByteUnits.Humanize(ByteUnits.MegaBytes + ByteUnits.MegaBytes / 2));
            Assert.Equal("1 GB", ByteUnits.Humanize(ByteUnits.GigaBytes));
            Assert.Equal("1.5 GB", ByteUnits.Humanize(ByteUnits.GigaBytes + ByteUnits.GigaBytes / 2));
            Assert.Equal("1 TB", ByteUnits.Humanize(ByteUnits.TeraBytes));
            Assert.Equal("1.5 TB", ByteUnits.Humanize(ByteUnits.TeraBytes + ByteUnits.TeraBytes / 2));
            Assert.Equal("1 PB", ByteUnits.Humanize(ByteUnits.PetaBytes));
            Assert.Equal("1.5 PB", ByteUnits.Humanize(ByteUnits.PetaBytes + ByteUnits.PetaBytes / 2));
            Assert.Equal("1 EB", ByteUnits.Humanize(ByteUnits.ExaBytes));
            Assert.Equal("1.5 EB", ByteUnits.Humanize(ByteUnits.ExaBytes + ByteUnits.ExaBytes / 2));

            // Verify that negative numbers are not supported.

            Assert.Throws<ArgumentException>(() => ByteUnits.Humanize(-1));
        }

        [Fact]
        public void Humanize_PowerOfTen_WithoutSpace()
        {
            Assert.Equal("0", ByteUnits.Humanize(0, spaceBeforeUnit: false));
            Assert.Equal("500", ByteUnits.Humanize(500, spaceBeforeUnit: false));
            Assert.Equal("1KB", ByteUnits.Humanize(ByteUnits.KiloBytes, spaceBeforeUnit: false));
            Assert.Equal("1.5KB", ByteUnits.Humanize(ByteUnits.KiloBytes + ByteUnits.KiloBytes / 2, spaceBeforeUnit: false));
            Assert.Equal("1MB", ByteUnits.Humanize(ByteUnits.MegaBytes, spaceBeforeUnit: false));
            Assert.Equal("1.5MB", ByteUnits.Humanize(ByteUnits.MegaBytes + ByteUnits.MegaBytes / 2, spaceBeforeUnit: false));
            Assert.Equal("1GB", ByteUnits.Humanize(ByteUnits.GigaBytes, spaceBeforeUnit: false));
            Assert.Equal("1.5GB", ByteUnits.Humanize(ByteUnits.GigaBytes + ByteUnits.GigaBytes / 2, spaceBeforeUnit: false));
            Assert.Equal("1TB", ByteUnits.Humanize(ByteUnits.TeraBytes, spaceBeforeUnit: false));
            Assert.Equal("1.5TB", ByteUnits.Humanize(ByteUnits.TeraBytes + ByteUnits.TeraBytes / 2, spaceBeforeUnit: false));
            Assert.Equal("1PB", ByteUnits.Humanize(ByteUnits.PetaBytes, spaceBeforeUnit: false));
            Assert.Equal("1.5PB", ByteUnits.Humanize(ByteUnits.PetaBytes + ByteUnits.PetaBytes / 2, spaceBeforeUnit: false));
            Assert.Equal("1EB", ByteUnits.Humanize(ByteUnits.ExaBytes, spaceBeforeUnit: false));
            Assert.Equal("1.5EB", ByteUnits.Humanize(ByteUnits.ExaBytes + ByteUnits.ExaBytes / 2, spaceBeforeUnit: false));

            // Verify that negative numbers are not supported.

            Assert.Throws<ArgumentException>(() => ByteUnits.Humanize(-1, spaceBeforeUnit: false));
        }

        [Fact]
        public void Humanize_PowerOfTen_NoB_WithSpace()
        {
            Assert.Equal("0", ByteUnits.Humanize(0, removeByteUnit: true));
            Assert.Equal("500", ByteUnits.Humanize(500, removeByteUnit: true));
            Assert.Equal("1 K", ByteUnits.Humanize(ByteUnits.KiloBytes, removeByteUnit: true));
            Assert.Equal("1.5 K", ByteUnits.Humanize(ByteUnits.KiloBytes + ByteUnits.KiloBytes / 2, removeByteUnit: true));
            Assert.Equal("1 M", ByteUnits.Humanize(ByteUnits.MegaBytes, removeByteUnit: true));
            Assert.Equal("1.5 M", ByteUnits.Humanize(ByteUnits.MegaBytes + ByteUnits.MegaBytes / 2, removeByteUnit: true));
            Assert.Equal("1 G", ByteUnits.Humanize(ByteUnits.GigaBytes, removeByteUnit: true));
            Assert.Equal("1.5 G", ByteUnits.Humanize(ByteUnits.GigaBytes + ByteUnits.GigaBytes / 2, removeByteUnit: true));
            Assert.Equal("1 T", ByteUnits.Humanize(ByteUnits.TeraBytes, removeByteUnit: true));
            Assert.Equal("1.5 T", ByteUnits.Humanize(ByteUnits.TeraBytes + ByteUnits.TeraBytes / 2, removeByteUnit: true));
            Assert.Equal("1 P", ByteUnits.Humanize(ByteUnits.PetaBytes, removeByteUnit: true));
            Assert.Equal("1.5 P", ByteUnits.Humanize(ByteUnits.PetaBytes + ByteUnits.PetaBytes / 2, removeByteUnit: true));
            Assert.Equal("1 E", ByteUnits.Humanize(ByteUnits.ExaBytes, removeByteUnit: true));
            Assert.Equal("1.5 E", ByteUnits.Humanize(ByteUnits.ExaBytes + ByteUnits.ExaBytes / 2, removeByteUnit: true));

            // Verify that negative numbers are not supported.

            Assert.Throws<ArgumentException>(() => ByteUnits.Humanize(-1));
        }

        [Fact]
        public void Humanize_PowerOfTen_WithoutSpace_NoB()
        {
            Assert.Equal("0", ByteUnits.Humanize(0, spaceBeforeUnit: false, removeByteUnit: true));
            Assert.Equal("500", ByteUnits.Humanize(500, spaceBeforeUnit: false));
            Assert.Equal("1K", ByteUnits.Humanize(ByteUnits.KiloBytes, spaceBeforeUnit: false, removeByteUnit: true));
            Assert.Equal("1.5K", ByteUnits.Humanize(ByteUnits.KiloBytes + ByteUnits.KiloBytes / 2, spaceBeforeUnit: false, removeByteUnit: true));
            Assert.Equal("1M", ByteUnits.Humanize(ByteUnits.MegaBytes, spaceBeforeUnit: false, removeByteUnit: true));
            Assert.Equal("1.5M", ByteUnits.Humanize(ByteUnits.MegaBytes + ByteUnits.MegaBytes / 2, spaceBeforeUnit: false, removeByteUnit: true));
            Assert.Equal("1G", ByteUnits.Humanize(ByteUnits.GigaBytes, spaceBeforeUnit: false, removeByteUnit: true));
            Assert.Equal("1.5G", ByteUnits.Humanize(ByteUnits.GigaBytes + ByteUnits.GigaBytes / 2, spaceBeforeUnit: false, removeByteUnit: true));
            Assert.Equal("1T", ByteUnits.Humanize(ByteUnits.TeraBytes, spaceBeforeUnit: false, removeByteUnit: true));
            Assert.Equal("1.5T", ByteUnits.Humanize(ByteUnits.TeraBytes + ByteUnits.TeraBytes / 2, spaceBeforeUnit: false, removeByteUnit: true));
            Assert.Equal("1P", ByteUnits.Humanize(ByteUnits.PetaBytes, spaceBeforeUnit: false, removeByteUnit: true));
            Assert.Equal("1.5P", ByteUnits.Humanize(ByteUnits.PetaBytes + ByteUnits.PetaBytes / 2, spaceBeforeUnit: false, removeByteUnit: true));
            Assert.Equal("1E", ByteUnits.Humanize(ByteUnits.ExaBytes, spaceBeforeUnit: false, removeByteUnit: true));
            Assert.Equal("1.5E", ByteUnits.Humanize(ByteUnits.ExaBytes + ByteUnits.ExaBytes / 2, spaceBeforeUnit: false, removeByteUnit: true));

            // Verify that negative numbers are not supported.

            Assert.Throws<ArgumentException>(() => ByteUnits.Humanize(-1, spaceBeforeUnit: false, removeByteUnit: true));
        }
    }
}

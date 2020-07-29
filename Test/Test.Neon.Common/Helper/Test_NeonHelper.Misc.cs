//-----------------------------------------------------------------------------
// FILE:	    Test_NeonHelper.Misc.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE, LLC.  All rights reserved.
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
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public partial class Test_NeonHelper
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ParseBool()
        {
            Assert.False(NeonHelper.ParseBool("0"));
            Assert.False(NeonHelper.ParseBool("off"));
            Assert.False(NeonHelper.ParseBool("no"));
            Assert.False(NeonHelper.ParseBool("disabled"));
            Assert.False(NeonHelper.ParseBool("false"));

            Assert.False(NeonHelper.ParseBool("0"));
            Assert.False(NeonHelper.ParseBool("Off"));
            Assert.False(NeonHelper.ParseBool("No"));
            Assert.False(NeonHelper.ParseBool("Disabled"));
            Assert.False(NeonHelper.ParseBool("False"));

            Assert.True(NeonHelper.ParseBool("1"));
            Assert.True(NeonHelper.ParseBool("on"));
            Assert.True(NeonHelper.ParseBool("yes"));
            Assert.True(NeonHelper.ParseBool("enabled"));
            Assert.True(NeonHelper.ParseBool("true"));

            Assert.True(NeonHelper.ParseBool("1"));
            Assert.True(NeonHelper.ParseBool("On"));
            Assert.True(NeonHelper.ParseBool("Yes"));
            Assert.True(NeonHelper.ParseBool("Enabled"));
            Assert.True(NeonHelper.ParseBool("True"));

            Assert.Throws<ArgumentNullException>(() => NeonHelper.ParseBool(null));
            Assert.Throws<ArgumentNullException>(() => NeonHelper.ParseBool(""));
            Assert.Throws<FormatException>(() => NeonHelper.ParseBool("   "));
            Assert.Throws<FormatException>(() => NeonHelper.ParseBool("ILLEGAL"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void TryParseBool()
        {
            bool value;

            Assert.True(NeonHelper.TryParseBool("0", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseBool("off", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseBool("no", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseBool("disabled", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseBool("false", out value));
            Assert.False(value);

            Assert.True(NeonHelper.TryParseBool("0", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseBool("Off", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseBool("No", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseBool("Disabled", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseBool("False", out value));
            Assert.False(value);

            Assert.True(NeonHelper.TryParseBool("1", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseBool("on", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseBool("yes", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseBool("enabled", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseBool("true", out value));
            Assert.True(value);

            Assert.True(NeonHelper.TryParseBool("1", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseBool("On", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseBool("Yes", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseBool("Enabled", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseBool("True", out value));
            Assert.True(value);

            Assert.False(NeonHelper.TryParseBool(null, out value));
            Assert.False(NeonHelper.TryParseBool("", out value));
            Assert.False(NeonHelper.TryParseBool("   ", out value));
            Assert.False(NeonHelper.TryParseBool("ILLEGAL", out value));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void ParseNullableBool()
        {
            Assert.False(NeonHelper.ParseNullableBool("0"));
            Assert.False(NeonHelper.ParseNullableBool("off"));
            Assert.False(NeonHelper.ParseNullableBool("no"));
            Assert.False(NeonHelper.ParseNullableBool("disabled"));
            Assert.False(NeonHelper.ParseNullableBool("false"));

            Assert.False(NeonHelper.ParseNullableBool("0"));
            Assert.False(NeonHelper.ParseNullableBool("Off"));
            Assert.False(NeonHelper.ParseNullableBool("No"));
            Assert.False(NeonHelper.ParseNullableBool("Disabled"));
            Assert.False(NeonHelper.ParseNullableBool("False"));

            Assert.True(NeonHelper.ParseNullableBool("1"));
            Assert.True(NeonHelper.ParseNullableBool("on"));
            Assert.True(NeonHelper.ParseNullableBool("yes"));
            Assert.True(NeonHelper.ParseNullableBool("enabled"));
            Assert.True(NeonHelper.ParseNullableBool("true"));

            Assert.True(NeonHelper.ParseNullableBool("1"));
            Assert.True(NeonHelper.ParseNullableBool("On"));
            Assert.True(NeonHelper.ParseNullableBool("Yes"));
            Assert.True(NeonHelper.ParseNullableBool("Enabled"));
            Assert.True(NeonHelper.ParseNullableBool("True"));

            Assert.Null(NeonHelper.ParseNullableBool(null));
            Assert.Null(NeonHelper.ParseNullableBool(""));

            Assert.Throws<FormatException>(() => NeonHelper.ParseNullableBool("   "));
            Assert.Throws<FormatException>(() => NeonHelper.ParseNullableBool("ILLEGAL"));
        }

        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void TryParseNullableBool()
        {
            bool? value;

            Assert.True(NeonHelper.TryParseNullableBool("0", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseNullableBool("off", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseNullableBool("no", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseNullableBool("disabled", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseNullableBool("false", out value));
            Assert.False(value);

            Assert.True(NeonHelper.TryParseNullableBool("0", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseNullableBool("Off", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseNullableBool("No", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseNullableBool("Disabled", out value));
            Assert.False(value);
            Assert.True(NeonHelper.TryParseNullableBool("False", out value));
            Assert.False(value);

            Assert.True(NeonHelper.TryParseNullableBool("1", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseNullableBool("on", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseNullableBool("yes", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseNullableBool("enabled", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseNullableBool("true", out value));
            Assert.True(value);

            Assert.True(NeonHelper.TryParseNullableBool("1", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseNullableBool("On", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseNullableBool("Yes", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseNullableBool("Enabled", out value));
            Assert.True(value);
            Assert.True(NeonHelper.TryParseNullableBool("True", out value));
            Assert.True(value);

            Assert.True(NeonHelper.TryParseNullableBool(null, out value));
            Assert.Null(value);
            Assert.True(NeonHelper.TryParseNullableBool("", out value));
            Assert.Null(value);

            Assert.False(NeonHelper.TryParseNullableBool("   ", out value));
            Assert.False(NeonHelper.TryParseNullableBool("ILLEGAL", out value));
        }
    }
}

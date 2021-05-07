//-----------------------------------------------------------------------------
// FILE:	    Test_LinuxPermissions.cs
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
using System.Threading.Tasks;

using Neon.Common;
using Neon.IO;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    [Trait(TestTrait.Category, TestArea.NeonCommon)]
    public class Test_LinuxPermissions
    {
        [Fact]
        public void TryParse()
        {
            LinuxPermissions permissions;

            Assert.True(LinuxPermissions.TryParse("700", out permissions));
            Assert.True(permissions.OwnerRead);
            Assert.True(permissions.OwnerWrite);
            Assert.True(permissions.OwnerExecute);
            Assert.False(permissions.GroupRead);
            Assert.False(permissions.GroupWrite);
            Assert.False(permissions.GroupExecute);
            Assert.False(permissions.AllRead);
            Assert.False(permissions.AllWrite);
            Assert.False(permissions.AllExecute);
            Assert.Equal("700", permissions.ToString());

            Assert.False(LinuxPermissions.TryParse("70x", out permissions));
        }

        [Fact]
        public void Constructor()
        {
            var permissions = new LinuxPermissions();

            Assert.False(permissions.OwnerRead);
            Assert.False(permissions.OwnerWrite);
            Assert.False(permissions.OwnerExecute);
            Assert.False(permissions.GroupRead);
            Assert.False(permissions.GroupWrite);
            Assert.False(permissions.GroupExecute);
            Assert.False(permissions.AllRead);
            Assert.False(permissions.AllWrite);
            Assert.False(permissions.AllExecute);
            Assert.Equal("000", permissions.ToString());

            permissions = new LinuxPermissions("700");

            Assert.True(permissions.OwnerRead);
            Assert.True(permissions.OwnerWrite);
            Assert.True(permissions.OwnerExecute);
            Assert.False(permissions.GroupRead);
            Assert.False(permissions.GroupWrite);
            Assert.False(permissions.GroupExecute);
            Assert.False(permissions.AllRead);
            Assert.False(permissions.AllWrite);
            Assert.False(permissions.AllExecute);
            Assert.Equal("700", permissions.ToString());

            permissions = new LinuxPermissions("070");

            Assert.False(permissions.OwnerRead);
            Assert.False(permissions.OwnerWrite);
            Assert.False(permissions.OwnerExecute);
            Assert.True(permissions.GroupRead);
            Assert.True(permissions.GroupWrite);
            Assert.True(permissions.GroupExecute);
            Assert.False(permissions.AllRead);
            Assert.False(permissions.AllWrite);
            Assert.False(permissions.AllExecute);
            Assert.Equal("070", permissions.ToString());

            permissions = new LinuxPermissions("007");

            Assert.False(permissions.OwnerRead);
            Assert.False(permissions.OwnerWrite);
            Assert.False(permissions.OwnerExecute);
            Assert.False(permissions.GroupRead);
            Assert.False(permissions.GroupWrite);
            Assert.False(permissions.GroupExecute);
            Assert.True(permissions.AllRead);
            Assert.True(permissions.AllWrite);
            Assert.True(permissions.AllExecute);
            Assert.Equal("007", permissions.ToString());

            permissions = new LinuxPermissions("123");

            Assert.False(permissions.OwnerRead);
            Assert.False(permissions.OwnerWrite);
            Assert.True(permissions.OwnerExecute);
            Assert.False(permissions.GroupRead);
            Assert.True(permissions.GroupWrite);
            Assert.False(permissions.GroupExecute);
            Assert.False(permissions.AllRead);
            Assert.True(permissions.AllWrite);
            Assert.True(permissions.AllExecute);
            Assert.Equal("123", permissions.ToString());

            permissions = new LinuxPermissions("000");

            Assert.False(permissions.OwnerRead);
            Assert.False(permissions.OwnerWrite);
            Assert.False(permissions.OwnerExecute);
            Assert.False(permissions.GroupRead);
            Assert.False(permissions.GroupWrite);
            Assert.False(permissions.GroupExecute);
            Assert.False(permissions.AllRead);
            Assert.False(permissions.AllWrite);
            Assert.False(permissions.AllExecute);
            Assert.Equal("000", permissions.ToString());
        }

        [Fact]
        public void Manual()
        {
            var permissions = new LinuxPermissions();

            permissions.OwnerRead = true;
            Assert.Equal("400", permissions.ToString());

            permissions.OwnerWrite = true;
            Assert.Equal("600", permissions.ToString());

            permissions.OwnerExecute = true;
            Assert.Equal("700", permissions.ToString());

            permissions.GroupRead = true;
            Assert.Equal("740", permissions.ToString());

            permissions.GroupWrite = true;
            Assert.Equal("760", permissions.ToString());

            permissions.GroupExecute = true;
            Assert.Equal("770", permissions.ToString());

            permissions.AllRead = true;
            Assert.Equal("774", permissions.ToString());

            permissions.AllWrite = true;
            Assert.Equal("776", permissions.ToString());

            permissions.AllExecute = true;
            Assert.Equal("777", permissions.ToString());
        }

        [Fact]
        public void Malformed()
        {
            Assert.Throws<ArgumentNullException>(() => new LinuxPermissions(null));
            Assert.Throws<ArgumentNullException>(() => new LinuxPermissions(""));
            Assert.Throws<ArgumentException>(() => new LinuxPermissions("800"));
            Assert.Throws<ArgumentException>(() => new LinuxPermissions("080"));
            Assert.Throws<ArgumentException>(() => new LinuxPermissions("008"));
            Assert.Throws<ArgumentException>(() => new LinuxPermissions("0"));
            Assert.Throws<ArgumentException>(() => new LinuxPermissions("00"));
            Assert.Throws<ArgumentException>(() => new LinuxPermissions("00a"));
        }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    Test_YugaByteFixture.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Xunit;
using Neon.Xunit.YugaByte;

using Xunit;

namespace TestYugaByte
{
    public class Test_YugaByteFixture : IClassFixture<YugaByteFixture>
    {
        private YugaByteFixture     fixture;

        public Test_YugaByteFixture(YugaByteFixture fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public void Connect_Cassandra()
        {
            // Verify that we can connect to the Cassandra interface.
        }

        [Fact]
        public void Connect_Postgres()
        {
            // Verify that we can connect to the Postgres interface.
        }
    }
}

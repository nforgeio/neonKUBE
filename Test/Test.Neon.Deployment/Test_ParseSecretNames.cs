//-----------------------------------------------------------------------------
// FILE:	    Test_ParseSecretNames.cs
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

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Xunit;

using Neon.Common;
using Neon.Deployment;
using Neon.IO;
using Neon.Xunit;

namespace TestDeployment
{
    [Trait(TestTrait.Area, TestArea.NeonDeployment)]
    [Collection(TestCollection.NonParallel)]
    [CollectionDefinition(TestCollection.NonParallel, DisableParallelization = true)]
    public partial class Test_ParseSecretNames
    {
        [Fact]
        public void Parse_Null()
        {
            Assert.Throws<ArgumentNullException>(() => ProfileServer.ParseSecretName(null));
        }

        [Fact]
        public void Parse_Empty()
        {
            Assert.Equal(string.Empty, ProfileServer.ParseSecretName(string.Empty).Name);
            Assert.Null(ProfileServer.ParseSecretName(string.Empty).Property);
        }

        [Fact]
        public void Parse_NoProperty()
        {
            Assert.Equal("test", ProfileServer.ParseSecretName("test").Name);
            Assert.Null(ProfileServer.ParseSecretName("test").Property);
        }

        [Fact]
        public void Parse_WithProperty()
        {
            Assert.Equal("test", ProfileServer.ParseSecretName("test[property]").Name);
            Assert.Equal("property", ProfileServer.ParseSecretName("test[property]").Property);
        }

        [Fact]
        public void Parse_BadProperty()
        {
            Assert.Equal("test[property", ProfileServer.ParseSecretName("test[property").Name);
            Assert.Null(ProfileServer.ParseSecretName("test[property").Property);

            Assert.Equal("testproperty]", ProfileServer.ParseSecretName("testproperty]").Name);
            Assert.Null(ProfileServer.ParseSecretName("testproperty]").Property);

            Assert.Equal("test", ProfileServer.ParseSecretName("test[]").Name);
            Assert.Null(ProfileServer.ParseSecretName("test[]").Property);
        }
    }
}

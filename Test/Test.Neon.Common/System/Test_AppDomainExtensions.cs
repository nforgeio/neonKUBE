//-----------------------------------------------------------------------------
// FILE:	    Test_AppDomainExtensions.cs
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
using System.IO;
using System.Net;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.Collections;
using Neon.Net;
using Neon.Retry;
using Neon.Xunit;

using Xunit;

namespace TestCommon
{
    public class Test_AppDomainExtensions
    {
        [Fact]
        [Trait(TestCategory.CategoryTrait, TestCategory.NeonCommon)]
        public void GetUserAssemblies()
        {
            // $note(jefflill):
            //
            // This test works only for .NET Core.  We're going to ignore the other 
            // framework platforms.

            if (NeonHelper.Framework == NetFramework.Core)
            {
                // Verify that we see no [System*] or [Microsoft*] related assemblies and
                // also that we scan types from the assemblies that are returned, to verify
                // that this method works around this issue:
                //
                //      https://github.com/nforgeio/neonKUBE/issues/531

                foreach (var assembly in AppDomain.CurrentDomain.GetUserAssemblies())
                {
                    Assert.NotEqual("System", assembly.FullName);
                    Assert.NotEqual("Microsoft", assembly.FullName);

                    Assert.False(assembly.FullName.StartsWith("System."));
                    Assert.False(assembly.FullName.StartsWith("Microsoft."));

                    assembly.GetTypes().ToArray();
                }
            }
        }
    }
}
//-----------------------------------------------------------------------------
// FILE:	    ModelGenTestHelper
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;

using Neon.ModelGen;
using Neon.Common;
using Neon.Xunit;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Xunit;

namespace TestModelGen
{
    /// <summary>
    /// Test helpers.
    /// </summary>
    internal static class ModelGenTestHelper
    {
        /// <summary>
        /// Adds the assembly references required to compile the generated code.
        /// </summary>
        /// <param name="references">The assembly references.</param>
        public static void ReferenceHandler(MetadataReferences references)
        {
            references.Add(typeof(System.Dynamic.CallInfo));
            references.Add(typeof(Newtonsoft.Json.JsonToken));
        }
    }
}

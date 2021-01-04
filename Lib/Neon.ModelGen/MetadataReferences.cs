//-----------------------------------------------------------------------------
// FILE:	    MetadataReferences.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime;
using System.Runtime.Serialization;
using System.Text;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Neon.ModelGen
{
    /// <summary>
    /// Specifies the metadata references to be used when compiling
    /// C# code.
    /// </summary>
    public class MetadataReferences : List<MetadataReference>
    {
        /// <summary>
        /// Adds the assembly holding a specific type to the references.
        /// </summary>
        /// <param name="type">The type.</param>
        public void Add(Type type)
        {
            Covenant.Requires<ArgumentNullException>(type != null, nameof(type));

            Add(MetadataReference.CreateFromFile(type.Assembly.Location));
        }
    }
}

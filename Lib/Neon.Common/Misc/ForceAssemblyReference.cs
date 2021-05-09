//-----------------------------------------------------------------------------
// FILE:	    ForceAssemblyReference.cs
// CONTRIBUTOR: Jeff Lill, Marcus Bowyer
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Diagnostics;

namespace Neon.Common
{
    /// <summary>
    /// Tag your assembly with this reference to force the compiler to include an assembly
    /// in the program output.  This is useful for situations where the assembly is required
    /// to be present even when it appears that the program doesn't actually reference any
    /// of the assembly's types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly)]
    public class ForceAssemblyReference :Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="type">Specifies a type in the assembly being reference.</param>
        public ForceAssemblyReference(Type type)
        {
            Covenant.Requires<ArgumentNullException>(type != null, nameof(type));
        }
    }
}

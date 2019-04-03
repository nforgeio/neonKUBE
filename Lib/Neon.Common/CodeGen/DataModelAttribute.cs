//-----------------------------------------------------------------------------
// FILE:	    DataModelAttribute.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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
using System.Text;

namespace Neon.CodeGen
{
    /// <summary>
    /// Used to provide the model code generator additional information
    /// about a specific data type.  Use of this optional because the code
    /// generator assumes that all types that are not specifically tagged
    /// by <see cref="ServiceModelAttribute"/> are data types.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class DataModelAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        public DataModelAttribute()
        {
        }

        /// <summary>
        /// <para>
        /// Optionally specifies the type identifier that will be used by
        /// generated code to identify the object type at runtime. This
        /// will be used when deserializing the object.
        /// </para>
        /// <para>
        /// This defaults to the fully qualified name of the type as it
        /// appears in the source assembly as it is scanned by the code 
        /// generator.  You may want to set this to reduce the length
        /// or just to customize how your data is persistedd.
        /// </para>
        /// </summary>
        public string Name { get; set; }
    }
}

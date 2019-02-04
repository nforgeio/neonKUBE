//-----------------------------------------------------------------------------
// FILE:	    DynamicIncludeAttribute.cs
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
using System.Linq;
using System.Reflection;
using System.Text;

using Newtonsoft.Json.Linq;

using Neon.Common;
using Neon.DynamicData;

namespace Neon.DynamicData
{
    /// <summary>
    /// Used to tag a <c>class</c> or <c>enum</c> such that the <b>entity-gen</b> Visual 
    /// Studio build tool will automatically include the type in the generated output.  This
    /// is somewhat limited, as described in the remarks.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Data models often need to reference <c>enum</c> types and sometimes its useful for a
    /// model library to be able to include constant definitions.  You can tag a class with
    /// this attribute to include the class in the generated model output.
    /// </para>
    /// <note>
    /// Only the class public constant definitions will be include in the generated output.
    /// </note>
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Enum)]
    public class DynamicIncludeAttribute : Attribute
    {
        /// <summary>
        /// Optional namespace for the generated class; otherwise the namespace
        /// will default to the namespace of the tagged class.
        /// </summary>
        public string Namespace { get; set; }

        /// <summary>
        /// Optionally indicates that the generated class will be declared as <c>internal</c>
        /// rather than <c>public</c>, the default.
        /// </summary>
        public bool IsInternal { get; set; }
    }
}

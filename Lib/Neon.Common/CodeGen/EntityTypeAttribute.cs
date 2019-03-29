//-----------------------------------------------------------------------------
// FILE:	    EntityTypeAttribute.cs
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
using System.Reflection;
using System.Text;

namespace Neon.CodeGen
{
    /// <summary>
    /// <para>
    /// Used to specify a string that uniquely identifies the entity type
    /// within the current database (e.g. within a Couchbase bucket) or
    /// other context.  This is used to  initialize the <b>__EntityType</b> 
    /// property for generated database entity classes.
    /// </para>
    /// <para>
    /// By default, this will set the entity type to the fully qualified
    /// name of the decorated type.
    /// </para>
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class EntityTypeAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="entityType">
        /// The entity type string.  Note that the the fully qualified
        /// name of the decorated data model will be used if this is <c>null</c>
        /// or empty.
        /// </param>
        public EntityTypeAttribute(string entityType = null)
        {
            this.EntityType = entityType;
        }

        /// <summary>
        /// The entity type string.  Note that the the fully qualified
        /// name of the decorated data model will be used if this is <c>null</c>
        /// or empty.
        /// </summary>
        public string EntityType { get; set; }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    EntityAttribute.cs
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
    /// Used to customize the database related code generated for the tagged
    /// data model interface.
    /// </para>
    /// <para>
    /// <see cref="EntityType"/> to specify a string that uniquely identifies 
    /// the entity type within the current database (e.g. within a Couchbase bucket) 
    /// or other context.  This is used to  initialize the <b>__ET</b> 
    /// property for generated database entity classes.  This defaults to setting
    /// will set the entity type to the fully qualified name of the data model.
    /// </para>
    /// <para>
    /// By default, a <c>public static string GetKey(param object[] args)</c> method
    /// is included in generated entity classes so that a database key for a specific
    /// entity can be easialy obtained.  This method simply generates a string by
    /// calling <see cref="object.ToString()"/> on all of arguments or using <b>"NULL"</b>
    /// for <c>null</c> values which each of these being separated by a single colon
    /// and then prepending the entire thing with the entity type.  The generated code 
    /// will look something like this:
    /// </para>
    /// <code lang="C#">
    /// public class PersonEntity : Entity&lt;Person&gt;
    /// {
    ///     public static string GetKey(params object[] args)
    ///     {
    ///         if (args.Length == 0)
    ///         {
    ///             throw new ArgumentException("At least one argument is expected.");
    ///         }
    ///         
    ///         var key = "entity-type::";
    ///         
    ///         for (int i=0; i $lt; args.Length; i++)
    ///         {
    ///             var arg = args[i];
    ///             
    ///             if (i &gt; 0)
    ///             {
    ///                 key += ":";
    ///             }
    ///             
    ///             key += arg != null ? arg.ToString() : "NULL";
    ///         }
    /// 
    ///         return key;
    ///     }
    ///     
    ///     ...
    /// }
    /// </code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class EntityAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="entityType">
        /// The entity type string.  Note that the the fully qualified
        /// name of the decorated data model will be used when this is 
        /// <c>null</c> or empty.
        /// </param>
        public EntityAttribute(string entityType = null)
        {
            this.EntityType = entityType;
        }

        /// <summary>
        /// The entity type string.  Note that the the fully qualified
        /// name of the decorated data model will be used when this is 
        /// <c>null</c> or empty.
        /// </summary>
        public string EntityType { get; set; }

        /// <summary>
        /// This property combined with <see cref="GetKeyString"/> is used
        /// to generate a <c>public static string GetKey(...)</c> method.
        /// See the class remarks for more information.
        /// </summary>
        public string GetKeyArgs { get; set; }

        /// <summary>
        /// This property combined with <see cref="GetKeyArgs"/> is used
        /// to generate a <c>public static string GetKey(...)</c> method.
        /// See the class remarks for more information.
        /// </summary>
        public string GetKeyString { get; set; }
    }
}

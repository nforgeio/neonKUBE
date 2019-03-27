//-----------------------------------------------------------------------------
// FILE:	    ServiceModelAttribute.cs
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
    /// Used to indicate that an <c>interface</c> should be included when
    /// generating a service client class.
    /// </summary>
    [AttributeUsage(AttributeTargets.Interface)]
    public class ServiceModelAttribute : Attribute
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="name">
        /// Optionally specifies the name to be used for the generated
        /// service client class.  This defaults to the tagged controller
        /// class name with a "Controller" suffix being stripped off if
        /// present.
        /// </param>
        /// <param name="group">
        /// <para>
        /// Optionally specifies that the methods from this controller
        /// should be grouped together in a generated controller class
        /// composed from multiple service controllers.  Set this to the
        /// name to be used for the client property under which these 
        /// methods will be generated.
        /// </para>
        /// <note>
        /// <paramref name="name"/> must also be specified when <paramref name="group"/>
        /// is set.
        /// </note>
        /// </param>
        public ServiceModelAttribute(string name = null, string group = null)
        {
            this.Name  = name;
            this.Group = group;

            if (!string.IsNullOrEmpty(group) && string.IsNullOrEmpty(name))
            {
                throw new ArgumentException($"[{nameof(name)}] cannot be empty when [{nameof(group)}] is specified.");
            }
        }

        /// <summary>
        /// <para>
        /// Returns the name to be used for the generated client class
        /// and for transmitting requests to the server or <c>null</c>
        /// if the name is to be derived from the tagged class name.
        /// </para>
        /// <note>
        /// The tagged controller class name will be used as the default
        /// name, stripping "Controller" off the end of the class name
        /// if present.
        /// </note>
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// <para>
        /// Optionally used to group multiple methods from different
        /// controllers with the same <see cref="Name"/> together
        /// into the same generated service client class or subclass.
        /// </para>
        /// <para>
        /// This defaults to <c>null</c> which means that the service
        /// methods from the different controllers will be generated
        /// directly within the generated service client.  When this
        /// is not <c>null</c> or empty, a subclass using this name
        /// with "Client" appended will be generated with the methods
        /// from the controllers with this group name.
        /// </para>
        /// </summary>
        public string Group { get; private set; }
    }
}
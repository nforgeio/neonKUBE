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
        /// <param name="controllerType">The service controller type.</param>
        public ServiceModelAttribute(Type controllerType)
        {
            Covenant.Requires(controllerType != null);

            this.ControllerType = controllerType;
        }

        /// <summary>
        /// Returns the source service controller type.
        /// </summary>
        public Type ControllerType { get; private set; }

        /// <summary>
        /// <para>
        /// The name to be used for the generated client class
        /// or <c>null</c> if a default name is to be used.  This
        /// can be used to group the methods from multiple service
        /// model (AKA controller) classes into a common generated
        /// service client.
        /// </para>
        /// <note>
        /// The tagged controller class name will be used as the default
        /// name, stripping "Controller" off the end of he class name
        /// if present.
        /// </note>
        /// </summary>
        public string Name { get; set; }

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
        public string Group { get; set; }
    }
}
//-----------------------------------------------------------------------------
// FILE:	    Extensions.cs
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

using Neon.Common;
using Neon.DynamicData;

namespace EntityGen
{
    /// <summary>
    /// Misc class extension methods.
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// Returns the parent interface for an entity interface definition.
        /// </summary>
        /// <param name="type">The interface type.</param>
        /// <returns>The parent interface or <c>null</c>.</returns>
        public static Type GetParentInterface(this Type type)
        {
            return type.GetTypeInfo().ImplementedInterfaces.FirstOrDefault();
        }

        /// <summary>
        /// Returns the root interface for an entity interface definition.
        /// </summary>
        /// <param name="type">The interface type.</param>
        /// <returns>The root interface.</returns>
        public static Type GetRootInterface(this Type type)
        {
            var rootType = type;

            while (true)
            {
                var parentType = rootType.GetParentInterface();

                if (parentType == null)
                {
                    break;
                }

                rootType = parentType;
            }

            return rootType;
        }
    }
}

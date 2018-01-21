//-----------------------------------------------------------------------------
// FILE:	    Extensions.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

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

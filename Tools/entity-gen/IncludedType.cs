//-----------------------------------------------------------------------------
// FILE:	    IncludedType.cs
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
    /// Information about an included <c>enum</c> or <c>class</c>.
    /// </summary>
    public class IncludedType
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="type">The original included type.</param>
        /// <param name="includeAttribute">The <see cref="DynamicIncludeAttribute"/> that tagged the type.</param>
        public IncludedType(Type type, DynamicIncludeAttribute includeAttribute)
        {
            this.Type       = type;
            this.Name       = type.Name;
            this.Namespace  = includeAttribute.Namespace ?? type.Namespace;
            this.IsInternal = includeAttribute.IsInternal;
        }

        /// <summary>
        /// Returns the generated type's name.
        /// </summary>
        public string Name { get; private set; }

        /// <summary>
        /// Returns the generated type's namespace.
        /// </summary>
        public string Namespace { get; private set; }

        /// <summary>
        /// Returns the generated type's fully qualified name.
        /// </summary>
        public string FullName
        {
            get { return $"{Namespace}.{Name}"; }
        }

        /// <summary>
        /// Returns <c>true</c> if the type is to be generated as <c>internal</c> rather than <c>public</c>.
        /// </summary>
        public bool IsInternal { get; private set; }

        /// <summary>
        /// Returns the original included type.
        /// </summary>
        public Type Type { get; private set; }
    }
}

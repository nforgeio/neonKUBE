//-----------------------------------------------------------------------------
// FILE:	    INormalizable.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.Serialization;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Neon.Docker
{
    /// <summary>
    /// Describes types that implement the <see cref="Normalize()"/> method that
    /// recursively ensures that any <b>null</b> class or list related properties 
    /// are replaced with instances with default values or empty lists.
    /// </summary>
    internal interface INormalizable
    {
        /// <summary>
        /// Recursively ensures ensures that any <b>null</b> class or list
        /// related properties are replaced with instances with default 
        /// values or empty lists.
        /// </summary>
        void Normalize();
    }
}

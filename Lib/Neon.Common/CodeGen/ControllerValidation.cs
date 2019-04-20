//-----------------------------------------------------------------------------
// FILE:	    ControllerValidationAttribute.cs
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
    /// <para>
    /// Used to have the <c>Neon.Xunit.XunitExtensions.ValidateController&lt;T&gt;()</c>
    /// method including the tagged method when validating the service controller
    /// against its definining interface.  This is useful for rare situations where a
    /// service controller inherits from another class that implements some endpoints.
    /// </para>
    /// <note>
    /// By default, <c>Neon.Xunit.XunitExtensions.ValidateController&lt;T&gt;()</c>
    /// only considers service methods implemented directly in the service controller
    /// during validation.
    /// </note>
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class ControllerValidationAttribute : Attribute
    {
    }
}

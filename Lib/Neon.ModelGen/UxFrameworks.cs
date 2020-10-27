//-----------------------------------------------------------------------------
// FILE:	    UxFrameworks.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2020 by neonFORGE LLC.  All rights reserved.
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

namespace Neon.ModelGen
{
    /// <summary>
    /// Enumerates the user interface frameworks that are supported with
    /// additional generated code.
    /// </summary>
    public enum UxFrameworks
    {
        /// <summary>
        /// Disables generation of additional UX related code.
        /// </summary>
        None = 0,

        /// <summary>
        /// Generate property and collection change notifications to support
        /// data binding for XAML UX frameworks.
        /// </summary>
        Xaml
    }
}

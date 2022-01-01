//-----------------------------------------------------------------------------
// FILE:	    WindowsFeatureStatus.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Common
{
    /// <summary>
    /// Enumerates the possible states of an optional Windows feature.
    /// </summary>
    public enum WindowsFeatureStatus
    {
        /// <summary>
        /// The feature status couldn't be determined.
        /// </summary>
        Unknown = 0,

        /// <summary>
        /// The feature is disabled.
        /// </summary>
        Disabled,

        /// <summary>
        /// The feature is enabled.
        /// </summary>
        Enabled,

        /// <summary>
        /// The feature is currently partially installed and will be enabled after
        /// Windows is restarted.
        /// </summary>
        EnabledPending
    }
}

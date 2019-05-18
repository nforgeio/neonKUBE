//-----------------------------------------------------------------------------
// FILE:	    ChildWorkflowPolicy .cs
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
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Cadence;
using Neon.Common;
using Neon.Retry;
using Neon.Time;

namespace Neon.Cadence
{
    /// <summary>
    /// Enumerates the possible child workflow behaviors when the parent
    /// workflow is terminated.
    /// </summary>
    public enum ChildWorkflowPolicy
    {
        /// <summary>
        /// <para>
        /// All open child workflows will be terminated when parent workflow is terminated.
        /// </para>
        /// <note>
        /// This policy is not implemented.
        /// </note>
        /// </summary>
        ChildWorkflowPolicyTerminate = 0,

        /// <summary>
        /// <para>
        /// Cancel requests will be sent to all open child workflows to all open child 
        /// workflows when parent workflow is terminated.
        /// </para>
        /// <note>
        /// This policy is not implemented.
        /// </note>
        /// </summary>
        ChildWorkflowPolicyRequestCancel = 1,

        /// <summary>
        /// ChildWorkflowPolicyAbandon is policy that will have no impact to child workflow execution when parent workflow is
        /// terminated.  This is the default policy.
        /// </summary>
        ChildWorkflowPolicyAbandon = 2
    }
}

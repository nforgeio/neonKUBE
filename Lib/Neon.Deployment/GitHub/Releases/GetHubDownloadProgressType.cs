//-----------------------------------------------------------------------------
// FILE:	    GetHubDownloadProgressType.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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

using System.Runtime.Serialization;

namespace Neon.Deployment
{
    /// <summary>
    /// Enumerates the types of progress indications raised when downloading 
    /// a multi-part file from a GitHub release.
    /// </summary>
    public enum GetHubDownloadProgressType
    {
        /// <summary>
        /// An existing local file is being verified.
        /// </summary>
        [EnumMember(Value = "check")]
        Check,

        /// <summary>
        /// The file is being downloaded.
        /// </summary>
        [EnumMember(Value = "download")]
        Download
    }
}

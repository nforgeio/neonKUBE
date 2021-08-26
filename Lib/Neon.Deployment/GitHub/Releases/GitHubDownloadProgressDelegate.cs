//-----------------------------------------------------------------------------
// FILE:	    GitHubDownloadProgressDelegate.cs
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

namespace Neon.Deployment
{
    /// <summary>
    /// Describes the callback used to monitor and possibly cancel the download
    /// of file composed from one or more assets from a GitHub Release.
    /// </summary>
    /// <param name="progressType">Passed indicating the current operation being performed.</param>
    /// <param name="percentComplete">Passed as the approximate percentage of the file downloaded (between 0..100).</param>
    /// <returns><c>true</c> if the download is to continue or <c>false</c> to cancel it.</returns>
    public delegate bool GitHubDownloadProgressDelegate(GetHubDownloadProgressType progressType,  int percentComplete);
}

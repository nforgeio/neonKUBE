//-----------------------------------------------------------------------------
// FILE:	    NotifyState.cs
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

using Microsoft.Net.Http.Server;

using Neon;
using Neon.Common;
using Neon.Cryptography;
using Neon.IO;
using Neon.Kube;
using Neon.Net;

namespace WinDesktop
{
    /// <summary>
    /// Describes an operation in progress or the current application state
    /// for the purpose of hold the text to be displayed in the balloon text
    /// and optional the notification icon animation.
    /// </summary>
    public class NotifyState
    {
        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="animatedIcon">The optional animation.</param>
        /// <param name="balloonText">The optional balloon text.</param>
        /// <param name="isTransient">
        /// Optionally indicates that the state is not associated with
        /// an operation indicating an error or other transient status.
        /// </param>
        /// <param name="isError">
        /// Optionally indicates that the application is in the error state.
        /// This implies <see cref="IsTransient"/><c>=true</c>.
        /// </param>
        public NotifyState(AnimatedIcon animatedIcon, string balloonText = null, bool isTransient = false, bool isError = false)
        {
            this.AnimatedIcon = animatedIcon;
            this.BalloonText  = balloonText;
            this.IsTransient  = isTransient || isError;
            this.IsError      = isError;
        }

        /// <summary>
        /// The optional animated icon.
        /// </summary>
        public AnimatedIcon AnimatedIcon { get; set; }

        /// <summary>
        /// The optional balloon text message.
        /// </summary>
        public string BalloonText { get; set; }

        /// <summary>
        /// Indicates that the state is not associated with an operation
        /// indicating an error or other transient status.
        /// </summary>
        public bool IsTransient { get; set; }

        /// <summary>
        /// Indicates that the client is in an error state.
        /// </summary>
        public bool IsError { get; set; }
    }
}

//-----------------------------------------------------------------------------
// FILE:	    ClickOffEventHandler.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:  	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Threading.Tasks;

namespace Neon.Tailwind
{
    /// <summary>
    /// Handler that manages click off events.
    /// </summary>
    public class ClickOffEventHandler : EventHandlerComponentBase<ClickOffEventHandler>
    {
        /// <summary>
        /// Callback that runs the click off handler.
        /// </summary>
        [Parameter] public EventCallback OnClickOff { get; set; }

        /// <summary>
        /// Invoke the click off callback.
        /// </summary>
        /// <returns></returns>
        [JSInvokable] 
        public Task HandleClickOff() => OnClickOff.InvokeAsync();

        /// <summary>
        /// Constructor.
        /// </summary>
        public ClickOffEventHandler() 
            : base("clickoffhandler", nameof(HandleClickOff)) 
        { 
        }
    }
}

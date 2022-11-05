//-----------------------------------------------------------------------------
// FILE:	    BottomMenu.razor.cs
// CONTRIBUTOR: Marcus Bowyer
// COPYRIGHT:   Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
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
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace NeonDashboard.Shared.Components
{
    public partial class BottomMenu : ComponentBase, IDisposable
    {
        [Inject]
        AppState AppState { get; set; }

        /// <summary>
        /// Constructor.
        /// </summary>
        public BottomMenu() { }

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            AppState.OnDashboardChange += StateHasChanged;
        }

        /// <inheritdoc />
        public void Dispose()
        {
            AppState.OnDashboardChange -= StateHasChanged;
        }
    }
}
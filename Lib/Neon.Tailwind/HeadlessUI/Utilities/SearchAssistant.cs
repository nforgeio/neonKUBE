//-----------------------------------------------------------------------------
// FILE:	    SearchAssistant.cs
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

using System;
using System.Timers;

namespace Neon.Tailwind
{
    public class SearchAssistant : IDisposable
    {
        public int DebouceTimeout { get; set; } = 350;
        public string SearchQuery { get; private set; } = "";

        public event EventHandler OnChange;

        private System.Timers.Timer debounceTimer;
        public void Search(string key)
        {
            SearchQuery += key;
            OnChange?.Invoke(this, EventArgs.Empty);
            StartDebounceTimer();
        }
        private void DebounceElapsed(object source, System.Timers.ElapsedEventArgs e)
        {
            ClearSearch();
            debounceTimer?.Dispose();
        }
        private void StartDebounceTimer()
        {
            ClearDebounceTimer();

            debounceTimer = new System.Timers.Timer(DebouceTimeout);
            debounceTimer.Elapsed += DebounceElapsed;
            debounceTimer.Enabled = true;
        }
        private void ClearDebounceTimer()
        {
            if (debounceTimer != null)
            {
                debounceTimer.Enabled = false;
                debounceTimer.Dispose();
                debounceTimer = null;
            }
        }
        public void ClearSearch()
        {
            ClearDebounceTimer();
            SearchQuery = "";
            OnChange?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose() => ClearSearch();
    }
}

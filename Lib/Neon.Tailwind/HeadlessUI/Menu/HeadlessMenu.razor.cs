//-----------------------------------------------------------------------------
// FILE:	    HeadlessMenu.razor.cs
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
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Neon.Tailwind
{
    public partial class HeadlessMenu : ComponentBase, IDisposable
    {
        [Parameter] public RenderFragment ChildContent { get; set; }

        [Parameter] public EventCallback OnOpen { get; set; }
        [Parameter] public EventCallback OnClose { get; set; }

        [Parameter] public int DebouceTimeout { get; set; } = 350;
        [Parameter(CaptureUnmatchedValues = true)] 
        public IReadOnlyDictionary<string, object> AdditionalAttributes { get; set; }

        private readonly List<HeadlessMenuItem> menuItems = new();
        private HeadlessMenuItem activeItem;
        private ClickOffEventHandler clickOffEventHandler;
        private SearchAssistant searchAssistant;

        private HeadlessMenuButton buttonElement;
        private HeadlessMenuItems itemsElement;

        public MenuState State { get; protected set; } = MenuState.Closed;
        public string SearchQuery => searchAssistant.SearchQuery;
        public string ActiveItemId => activeItem?.Id;
        public string ButtonElementId => buttonElement?.Id;
        public string ItemsElementId => itemsElement?.Id;

        /// <summary>
        /// Constructor.
        /// </summary>
        public HeadlessMenu()
        {
            searchAssistant = new SearchAssistant();
            searchAssistant.OnChange += HandleSearchChange!;
        }

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (shouldFocus)
            {
                shouldFocus = false;
                if (State == MenuState.Open)
                {
                    await MenuItemsFocusAsync();
                }
                else
                {
                    //I wouldn't think the Task.Yield would be necessary but Blazor occationally throws a javascript error that I am unable to isolate if it isn't in there
                    //If we can identify the precise cause of the error then this could be removed.

                    await Task.Yield();
                    await ButtonFocusAsync();
                }
            }
            if (clickOffEventHandler != null)
            {
                await clickOffEventHandler.RegisterElement(buttonElement!);
                await clickOffEventHandler.RegisterElement(itemsElement!);
            }
        }

        public void RegisterItem(HeadlessMenuItem item) => menuItems.Add(item);
        public void UnregisterItem(HeadlessMenuItem item)
        {
            if (!menuItems.Contains(item)) return;

            if (IsActiveItem(item))
                GoToItem(MenuFocus.Next);

            menuItems.Remove(item);
        }
        public bool IsActiveItem(HeadlessMenuItem item) => activeItem == item;
        public void GoToItem(HeadlessMenuItem item)
        {
            if (item != null && (!item.IsEnabled || !menuItems.Contains(item))) item = null;
            if (activeItem == item) return;

            activeItem = item;
            StateHasChanged();
        }

        public void GoToItem(MenuFocus focus)
        {
            switch (focus)
            {
                case MenuFocus.First:
                    GoToItem(menuItems.FirstOrDefault(mi => mi.IsEnabled));
                    break;
                case MenuFocus.Previous:
                    GoToItem(FindItemBeforeActiveItem());
                    break;
                case MenuFocus.Next:
                    GoToItem(FindItemAfterActiveItem());
                    break;
                case MenuFocus.Last:
                    GoToItem(menuItems.LastOrDefault(mi => mi.IsEnabled));
                    break;
                default:
                    GoToItem(null);
                    break;
            }
        }
        private HeadlessMenuItem FindItemBeforeActiveItem()
        {
            var reversedMenuOptions = menuItems.ToList();
            reversedMenuOptions.Reverse();
            bool foundTarget = false;
            var itemIndex = reversedMenuOptions.FindIndex(0, mi =>
            {
                if (mi == activeItem)
                {
                    foundTarget = true;
                    return false;
                }
                return foundTarget && mi.IsEnabled;
            });
            if (itemIndex != -1)
                return reversedMenuOptions[itemIndex];
            else
                return menuItems.LastOrDefault(mi => mi.IsEnabled);
        }
        private HeadlessMenuItem FindItemAfterActiveItem()
        {
            bool foundTarget = false;
            var itemIndex = menuItems.FindIndex(0, mi =>
            {
                if (mi == activeItem)
                {
                    foundTarget = true;
                    return false;
                }
                return foundTarget && mi.IsEnabled;
            });
            if (itemIndex != -1)
                return menuItems[itemIndex];
            else
                return menuItems.FirstOrDefault(mi => mi.IsEnabled);
        }

        public void RegisterButton(HeadlessMenuButton button)
            => buttonElement = button;
        public void RegisterItems(HeadlessMenuItems items)
            => itemsElement = items;

        private bool shouldFocus;
        public async Task Toggle()
        {
            if (State == MenuState.Closed)
                await Open();
            else
                await Close();
        }
        public async Task Open()
        {
            if (State == MenuState.Open) return;
            State = MenuState.Open;
            await OnOpen.InvokeAsync();
            shouldFocus = true;
            StateHasChanged();
        }
        public async Task Close(bool suppressFocus = false)
        {
            if (State == MenuState.Closed) return;
            State = MenuState.Closed;
            await OnClose.InvokeAsync();
            shouldFocus = !suppressFocus;
            StateHasChanged();
        }

        public ValueTask ButtonFocusAsync() => buttonElement?.FocusAsync() ?? ValueTask.CompletedTask;
        public ValueTask MenuItemsFocusAsync() => itemsElement?.FocusAsync() ?? ValueTask.CompletedTask;

        public Task HandleClickOff() => Close();
        private void HandleSearchChange(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(SearchQuery))
            {
                var item = menuItems.FirstOrDefault(mi => (mi.SearchValue ?? "").StartsWith(SearchQuery, StringComparison.OrdinalIgnoreCase) && mi.IsEnabled);
                GoToItem(item);
            }
        }
        public void Search(string key)
        {
            searchAssistant.Search(key);
        }

        public void Dispose() => searchAssistant.Dispose();
    }
}

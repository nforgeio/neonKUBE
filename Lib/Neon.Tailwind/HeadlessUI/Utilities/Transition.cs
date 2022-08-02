//-----------------------------------------------------------------------------
// FILE:	    Transition.cs
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
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Timers;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.Extensions.Logging;

using Neon.Tasks;

namespace Neon.Tailwind
{
    public class Transition : ComponentBase
    {
        [CascadingParameter] 
        public TransitionGroup TransitionGroup { get; set; }

        [Parameter] 
        public RenderFragment<string> ChildContent { get; set; }


        /// <summary>
        /// Classes to add to the transitioning element during the entire enter phase.
        /// </summary>
        [Parameter]
        public string Enter { get; set; }

        /// <summary>
        /// The starting point to enter from.
        /// </summary>
        [Parameter]
        public string EnterFrom { get; set; }

        /// <summary>
        /// The ending point to enter to.
        /// </summary>
        [Parameter]
        public string EnterTo { get; set; }

        /// <summary>
        /// The duration that the enter transition will take.
        /// </summary>
        [Parameter]
        public int? EnterDuration { get; set; }

        /// <summary>
        /// Classes to add to the transitioning element during the entire leave phase.
        /// </summary>
        [Parameter]
        public string Leave { get; set; }

        /// <summary>
        /// Classes to add to the transitioning element before the leave phase starts.
        /// </summary>
        [Parameter]
        public string LeaveFrom { get; set; }

        /// <summary>
        /// Classes to add to the transitioning element immediately after the leave phase starts.
        /// </summary>
        [Parameter]
        public string LeaveTo { get; set; }

        /// <summary>
        /// The duration that the leave transition will take.
        /// </summary>
        [Parameter]
        public int? LeaveDuration { get; set; }

        /// <summary>
        /// Whether the transition should run on initial mount.
        /// </summary>
        [Parameter]
        public bool Show { get; set; } = false;
        [Parameter] 
        public EventCallback<bool> BeginTransition { get; set; }
        [Parameter] 
        public EventCallback<bool> EndTransition { get; set; }

        [Parameter(CaptureUnmatchedValues = true)]
        public IReadOnlyDictionary<string, object> Attributes { get; set; }

        public event Action OnTransitionChange;
        private void NotifyTransitionChanged() => InvokeAsync(OnTransitionChange);

        public TransitionState State { get; set; }
        public string CurrentCssClass { get; private set; }
        public string ClassAttributes { get; private set; }

        private bool transitionStarted;
        private System.Timers.Timer transitionTimer;
        private bool stateChangeRequested;
        private string enter;
        private int enterDuration;
        private string enterDurationString;
        private string leave;
        private int leaveDuration;
        private string leaveDurationString;

        /// <inheritdoc/>
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (TransitionHasStartedOrCompleted()) return;
            await StartTransition();
        }

        private void GetClassAttributes()
        {
            if (Attributes != null)
            {
                try
                {
                    if (Attributes.TryGetValue("class", out var classAttributes))
                    {
                        ClassAttributes = (string)classAttributes;
                    }
                }
                catch
                {

                }
            }
        }

        private async Task StartTransition()
        {
            transitionStarted = true;

            //Not sure why this is required when showing but I am guessing it allows blazor to finish the actual
            //dom manipulation of adding the item to the page before we start a new state
            await Task.Yield();

            var cssClass = new StringBuilder();

            if (!string.IsNullOrEmpty(ClassAttributes))
            {
                cssClass.Append(ClassAttributes);
            }

            switch (State)
            {
                case TransitionState.Entering:

                    cssClass.Append($" {enter}");
                    cssClass.Append($" {enterDurationString}");
                    cssClass.Append($" {EnterTo}");
                    break;

                case TransitionState.Leaving:
                default:

                    cssClass.Append($" {leave}");
                    cssClass.Append($" {leaveDurationString}");
                    cssClass.Append($" {LeaveTo}");
                    break;
            }

            CurrentCssClass = cssClass.ToString();

            _ = BeginTransition.InvokeAsync();

            StartTransitionTimer();
            NotifyTransitionChanged();
        }

        private bool TransitionHasStartedOrCompleted() => State == TransitionState.Visible || State == TransitionState.Hidden || transitionStarted;

        private void StartTransitionTimer()
        {
            switch (State)
            {
                case TransitionState.Entering:
                    
                    if (enterDuration <= 0)
                    {
                        return;
                    }
                    transitionTimer = new System.Timers.Timer(enterDuration);
                    break;

                case TransitionState.Leaving:
                default:

                    if (leaveDuration <= 0)
                    {
                        return;
                    }
                    transitionTimer = new Timer(leaveDuration);
                    break;
            }
            
            transitionTimer.Elapsed   += OnEndTransition;
            transitionTimer.AutoReset = false;
            transitionTimer.Enabled   = true;
        }

        private void OnEndTransition(object source, ElapsedEventArgs e)
        {
            State = State == TransitionState.Entering ? TransitionState.Visible : TransitionState.Hidden;
            ClearCurrentTransition();

            TransitionGroup?.NotifyEndTransition();
            EndTransition.InvokeAsync();

            NotifyTransitionChanged();
        }

        private void ClearCurrentTransition()
        {
            CurrentCssClass = ClassAttributes;
            transitionStarted = false;

            if (transitionTimer == null) return;
            transitionTimer.Dispose();
            transitionTimer = null;
        }

        /// <inheritdoc/>
        public override async Task SetParametersAsync(ParameterView parameters)
        {
            var currentShowValue = Show;            

            parameters.SetParameterProperties(this);
            
            Show = TransitionGroup?.Show ?? Show;
            stateChangeRequested = currentShowValue != Show;
            await base.SetParametersAsync(ParameterView.Empty);
        }

        /// <inheritdoc/>
        protected override void OnParametersSet()
        {
            if (!stateChangeRequested) return;
            
            stateChangeRequested = false;

            GetClassAttributes();

            string durationPattern = @"duration[-[]+([0-9]+)[a-z\]]*";

            if (!string.IsNullOrEmpty(Enter))
            {
                if (!EnterDuration.HasValue)
                {
                    var match = Regex.Match(Enter, durationPattern);
                    if (match.Success)
                    {
                        enterDuration = int.Parse(match.Groups[1].Value);
                    }
                    else
                    {
                        enterDuration = 0;
                        enterDurationString = string.Empty;
                    }
                }
                else
                {
                    enterDuration = EnterDuration.Value;
                }

                enterDurationString = $"duration-[{enterDuration}ms]";
                enter = Regex.Replace(Enter, durationPattern, "");
            }

            if (!string.IsNullOrEmpty(Leave))
            {
                if (!LeaveDuration.HasValue)
                {
                    var match = Regex.Match(Leave, durationPattern);
                    if (match.Success)
                    {
                        leaveDuration = int.Parse(match.Groups[1].Value);
                    }
                    else
                    {
                        leaveDuration = 0;
                        leaveDurationString = string.Empty;
                    }
                }
                else
                {
                    leaveDuration = LeaveDuration.Value;
                }

                leaveDurationString = $"duration-[{leaveDuration}ms]";
                leave = Regex.Replace(Leave, durationPattern, "");
            }

            if (Show)
                InitializeEntering();
            else
                InitializeLeaving();
        }

        /// <inheritdoc/>
        protected override void OnInitialized()
        {
            TransitionGroup?.RegisterTransition(this);
            OnTransitionChange += StateHasChanged;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            OnTransitionChange -= StateHasChanged;
        }

        private void InitializeEntering()
        {
            ClearCurrentTransition();

            if (EnterDuration == 0)
            {
                State = TransitionState.Visible;
                return;
            }

            string attributeClass = string.Empty;
            if (Attributes != null)
            {
                attributeClass = (string)Attributes["class"];
            }

            State = TransitionState.Entering;

            var cssClass = new StringBuilder();

            if (!string.IsNullOrEmpty(ClassAttributes))
            {
                cssClass.Append(ClassAttributes);
            }
            cssClass.Append($" {enter}");
            cssClass.Append($" {enterDurationString}");
            cssClass.Append($" {EnterFrom}");

            CurrentCssClass = cssClass.ToString();
        }
        private void InitializeLeaving()
        {
            ClearCurrentTransition();

            if (LeaveDuration == 0)
            {
                State = TransitionState.Leaving;
                return;
            }

            State = TransitionState.Leaving;

            var cssClass = new StringBuilder();

            if (!string.IsNullOrEmpty(ClassAttributes))
            {
                cssClass.Append(ClassAttributes);
            }
            cssClass.Append($" {leave}");
            cssClass.Append($" {leaveDurationString}");
            cssClass.Append($" {LeaveFrom}");

            CurrentCssClass = cssClass.ToString();
        }

        /// <inheritdoc/>
        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            if (State != TransitionState.Hidden)
            {
                builder.AddContent(0, ChildContent, CurrentCssClass);
            }
        }

        public void Open()
        {
            if (State == TransitionState.Visible || State == TransitionState.Entering) return;
            InitializeEntering();
            NotifyTransitionChanged();
        }
        public void Close()
        {
            if (State == TransitionState.Leaving || State == TransitionState.Hidden) return;
            InitializeLeaving();
            NotifyTransitionChanged();
        }
        public void Toggle()
        {
            if (State == TransitionState.Visible || State == TransitionState.Entering)
                Close();
            else
                Open();
        }
    }
}
//-----------------------------------------------------------------------------
// FILE:	    ComponentEventArgs.cs
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

namespace Neon.Tailwind
{
    public class ComponentEventArgs<TSender, TEventArgs> : ComponentEventArgs<TSender>
    {
        public ComponentEventArgs(TSender sender, TEventArgs eventArgs)
            : base(sender)
        {
            EventArgs = eventArgs;
        }

        public TEventArgs EventArgs { get; }

        public void Deconstruct(out TSender sender, out TEventArgs eventArgs)
        {
            sender = Sender;
            eventArgs = EventArgs;
        }

        public static implicit operator TEventArgs(ComponentEventArgs<TSender, TEventArgs> eventArgs) => eventArgs.EventArgs;
        public static implicit operator ComponentEventArgs<TSender, TEventArgs>((TSender, TEventArgs) eventArgs) => new ComponentEventArgs<TSender, TEventArgs>(eventArgs.Item1, eventArgs.Item2);
    }

    public class ComponentEventArgs<TSender>
    {
        public ComponentEventArgs(TSender sender)
        {
            Sender = sender;
        }

        public TSender Sender { get; }

        public static implicit operator TSender(ComponentEventArgs<TSender> eventArgs) => eventArgs.Sender;
        public static implicit operator ComponentEventArgs<TSender>(TSender sender) => new ComponentEventArgs<TSender>(sender);
    }
}

//-----------------------------------------------------------------------------
// FILE:	    Listbox.razor.cs
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
using System.Linq;

using Microsoft.AspNetCore.Components;

namespace TestTailwind.Components
{
    public partial class Listbox : ComponentBase
    {
        private Person selectedPerson;

        private List<Person> people = new()
        {
            new() { Id = 1, Name = "Durward Reynolds" },
            new() { Id = 2, Name = "Kenton Towne", Enabled = false },
            new() { Id = 3, Name = "Therese Wunsch" },
            new() { Id = 4, Name = "Benedict Kegler" },
            new() { Id = 5, Name = "Katelyn Rohan" }
        };

        private record Person
        {
            public int Id { get; init; }
            public string Name { get; init; }
            public bool Enabled { get; init; } = true;
        }
    }
}

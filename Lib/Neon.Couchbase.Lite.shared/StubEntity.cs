//-----------------------------------------------------------------------------
// FILE:	    StubEntity.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.DynamicData;

namespace Couchbase.Lite
{
    /// <summary>
    /// Used internally to access the static <see cref="EntityDocument{TEntity}"/> methods.
    /// </summary>
    internal class StubEntity : IDynamicEntity
    {
        #pragma warning disable 1591, 0067

        public JObject JObject
        {
            get
            {
                return null;
            }
        }

        public event EventHandler<EventArgs> Changed;
        public event PropertyChangedEventHandler PropertyChanged;

        public void _Attach(IDynamicEntity parent)
        {
        }

        public void _Detach()
        {
        }

        public string _GetEntityType()
        {
            return null;
        }

        public string _GetLink()
        {
            return null;
        }

        public bool _Load(JObject jObject, bool reload = false, bool setType = true)
        {
            return false;
        }

        public void _OnChanged()
        {
        }

        public void _OnPropertyChanged(string propertyName)
        {
        }

        public void _SetLink(string link)
        {
        }
    }
}

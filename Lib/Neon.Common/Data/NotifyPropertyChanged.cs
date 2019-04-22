//-----------------------------------------------------------------------------
// FILE:	    NotifyPropertyChanged.cs
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
// limitations under the License.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neon.Data
{
    /// <summary>
    /// A common implementation of <see cref="INotifyPropertyChanged"/>.
    /// </summary>
    public abstract class NotifyPropertyChanged : INotifyPropertyChanged
    {
        /// <summary>
        /// Raised when an instance property value has changed.
        /// </summary>
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Derived classes will call this when an property instance property value has changed.
        /// </summary>
        /// <param name="propertyName">
        /// The optional property name.  This defaults to the name of the caller, typically
        /// the property's setter.  This may also be passed as <c>null</c> signalling that
        /// all instance properties may have changed.
        /// </param>
        protected void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

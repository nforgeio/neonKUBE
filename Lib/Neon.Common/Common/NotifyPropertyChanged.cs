//-----------------------------------------------------------------------------
// FILE:	    NotifyPropertyChanged.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2018 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace Neon.Common
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
        /// the property's setter.
        /// </param>
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}

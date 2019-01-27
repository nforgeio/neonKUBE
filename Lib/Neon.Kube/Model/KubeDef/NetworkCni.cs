//-----------------------------------------------------------------------------
// FILE:	    NetworkCni.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2019 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Runtime.Serialization;

namespace Neon.Kube
{
    /// <summary>
    /// Enumerates the supported of cluster network providers.
    /// </summary>
    public enum NetworkCni
    {
        /// <summary>
        /// The <a href="https://projectcalico.org">Calico</a> network provider.  As of 01/2019, this is probably
        /// the most popular network provider.  This is currently the default provider deployed for a neonKUBE
        /// but we expect to change this to the <see cref="Istio"/> integrated provider when that is ready.
        /// </summary>
        [EnumMember(Value = "calico")]
        Calico = 0,

        /// <summary>
        /// The <a href="https://istio.io">Istio</a> integrated provider.  This isn't quite ready for prime time
        /// yet but will eventually become the default provider.
        /// </summary>
        [EnumMember(Value = "istio")]
        Istio,
    }
}

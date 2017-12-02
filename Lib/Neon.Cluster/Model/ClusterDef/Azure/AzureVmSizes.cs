//-----------------------------------------------------------------------------
// FILE:	    AzureVmSizes.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2017 by neonFORGE, LLC.  All rights reserved.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Serialization;

using Neon.Common;
using Neon.Net;

namespace Neon.Cluster
{
    /// <summary>
    /// Enumerates the possible Azure VM sizes.
    /// </summary>
    public enum AzureVmSizes
    {
        #pragma warning disable 1591 // Disable code comment warnings

        Standard_A1,
        Standard_A2,
        Standard_A3,
        Standard_A4,
        Standard_A5,
        Standard_A6,
        Standard_A7,

        Standard_D1_v2,
        Standard_D2_v2,
        Standard_D3_v2,
        Standard_D4_v2,
        Standard_D5_v2,
        Standard_D11_v2,
        Standard_D12_v2,
        Standard_D13_v2,
        Standard_D14_v2,
        Standard_D15_v2,

        Standard_DS1_v2,
        Standard_DS2_v2,
        Standard_DS3_v2,
        Standard_DS4_v2,
        Standard_DS5_v2,
        Standard_DS11_v2,
        Standard_DS12_v2,
        Standard_DS13_v2,
        Standard_DS14_v2,
        Standard_DS15_v2,

        Standard_G1,
        Standard_G2,
        Standard_G3,
        Standard_G4,
        Standard_G5,

        Standard_GS1,
        Standard_GS2,
        Standard_GS3,
        Standard_GS4,
        Standard_GS5,

        Standard_F1,
        Standard_F2,
        Standard_F4,
        Standard_F8,
        Standard_F16,

        Standard_F1s,
        Standard_F2s,
        Standard_F4s,
        Standard_F8s,
        Standard_F16s,
    }
}
//-----------------------------------------------------------------------------
// FILE:	    AzureVmSizes.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2016-2020 by neonFORGE, LLC.  All rights reserved.
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

namespace Neon.Kube
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

        Standard_A1_v2,
        Standard_A2_v2,
        Standard_A4_v2,
        Standard_A8_v2,
        Standard_A2M_v2,
        Standard_A4M_v2,
        Standard_A8M_v2,

        Standard_B1S,
        Standard_B1MS,
        Standard_B2S,
        Standard_B2MS,
        Standard_B4MS,
        Standard_B8MS,

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

        Standard_DC2S,
        Standard_DC4S,

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

        Standard_D2S_v3,
        Standard_D4S_v3,
        Standard_D8s_v3,
        Standard_D16S_v3,
        Standard_D32S_v3,
        Standard_D64S_v3,

        Standard_E2_v3,
        Standard_E4_v3,
        Standard_E8_v3,
        Standard_E16_v3,
        Standard_E20_v3,
        Standard_E32_v3,
        Standard_E64_v3,
        Standard_E64I_v3,

        Standard_ES2_v3,
        Standard_ES4_v3,
        Standard_ES8_v3,
        Standard_ES16_v3,
        Standard_ES20_v3,
        Standard_ES32_v3,
        Standard_ES64_v3,
        Standard_ES64I_v3,

        Standard_F1,
        Standard_F2,
        Standard_F4,
        Standard_F8,
        Standard_F16,

        Standard_F1S,
        Standard_F2S,
        Standard_F4S,
        Standard_F8S,
        Standard_F16S,

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

        Standard_F2S_v2,
        Standard_F4S_v2,
        Standard_F8S_v2,
        Standard_F16S_v2,
        Standard_F32S_v2,
        Standard_F64S_v2,
        Standard_F72S_v2,

        Standard_H8,
        Standard_H8M,
        Standard_H16,
        Standard_H16M,
        Standard_H16R,
        Standard_H16MR,

        Standard_L4S,
        Standard_L8S,
        Standard_L16S,
        Standard_L32S,

        Standard_M8MS,
        Standard_M16MS,
        Standard_M32LS,
        Standard_M32TS,
        Standard_M32MS,
        Standard_M64,
        Standard_M64MS,
        Standard_M64M,
        Standard_M64S,
        Standard_M64LS,
        Standard_M128,
        Standard_M128M,
        Standard_M128MS,
        Standard_M128S,

        Standard_NC6,
        Standard_NC12,
        Standard_NC24,
        Standard_NC24R,

        Standard_NC6S_v2,
        Standard_NC12S_v2,
        Standard_NC24S_v2,
        Standard_NC24RS_v2,

        Standard_NC6S_v3,
        Standard_NC12S_v3,
        Standard_NC24S_v3,
        Standard_NC24RS_v3,

        Standard_ND6S,
        Standard_ND12S,
        Standard_ND24S,
        Standard_ND24RS,

        Standard_NV6,
        Standard_NV12,
        Standard_NV24,
    }
}
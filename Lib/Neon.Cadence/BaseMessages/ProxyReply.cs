//-----------------------------------------------------------------------------
// FILE:	    ProxyReply.cs
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
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;

using Newtonsoft.Json;
using YamlDotNet.Serialization;

using Neon.Common;

namespace Neon.Cadence
{
    /// <summary>
    /// Base class for all proxy requests.
    /// </summary>
    [ProxyMessage(MessageType.Unspecified)]
    internal class ProxyReply : ProxyMessage
    {
        /// <summary>
        /// Uniquely identifies the request this reply answers.
        /// </summary>
        public string RequestId
        {
            get => GetStringProperty("RequestId");
            set => SetStringProperty("RequestId", value);
        }

        /// <summary>
        /// Indicates the error type.
        /// </summary>
        public CadenceErrorTypes ErrorType
        {
            get
            {
                switch (GetStringProperty("ErrorType"))
                {
                    case null:          return CadenceErrorTypes.None;
                    case "cancelled":   return CadenceErrorTypes.Cancelled;
                    case "custom":      return CadenceErrorTypes.Custom;
                    case "generic":     return CadenceErrorTypes.Generic;
                    case "panic":       return CadenceErrorTypes.Panic;
                    case "terminated":  return CadenceErrorTypes.Terminated;
                    case "timeout":     return CadenceErrorTypes.Timeout;

                    default:

                        throw new NotImplementedException();
                }
            }

            set
            {
                string typeString;

                switch (value)
                {
                    case CadenceErrorTypes.None:        typeString = null;          break;
                    case CadenceErrorTypes.Cancelled:   typeString = "cancelled";   break;
                    case CadenceErrorTypes.Custom:      typeString = "custom";      break;
                    case CadenceErrorTypes.Generic:     typeString = "generic";     break;
                    case CadenceErrorTypes.Panic:       typeString = "panic";       break;
                    case CadenceErrorTypes.Terminated:  typeString = "terminated";  break;
                    case CadenceErrorTypes.Timeout:     typeString = "timeout";     break;

                    default:

                        throw new NotImplementedException();
                }

                SetStringProperty("ErrorType", typeString);
            }
        }

        /// <summary>
        /// Describes the error in more detail.
        /// </summary>
        public string ErrorMessage
        {
            get => GetStringProperty("ErrorMessage");
            set => SetStringProperty("ErrorMessage", value);
        }
    }
}

﻿//-----------------------------------------------------------------------------
// FILE:	    Stub.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2022 by neonFORGE LLC.  All rights reserved.
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

using DotnetKubernetesClient.Entities;
using k8s.Models;
using KubeOps.Operator.Entities;
using KubeOps.Operator.Entities.Annotations;

using Neon.Kube;

namespace Neon.Kube.Resources
{
    /// <summary>
    /// Used for obtaining a reference to the <b>Neon.Kube.Resources</b> assembly
    /// by our Kubernetes operators for registering custom resource types.
    /// </summary>
    public static class Stub
    {
    }
}
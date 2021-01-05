//-----------------------------------------------------------------------------
// FILE:        ContainerLimits.cs
// CONTRIBUTOR: Jeff Lill
// COPYRIGHT:	Copyright (c) 2005-2021 by neonFORGE LLC.  All rights reserved.
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
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

using Xunit;

using Neon.Common;

namespace Neon.Xunit
{
    /// <summary>
    /// <para>
    /// Used by same Docker related (and derived) fixtures to limit the machine 
    /// resources that can be consumed by managed containers.  We support many
    /// of the limits described in detail bere:
    /// </para>
    /// <para>
    /// <a href="https://docs.docker.com/config/containers/resource_constraints/">https://docs.docker.com/config/containers/resource_constraints/</a>
    /// </para>
    /// <note>
    /// Byte size properties like <see cref="Memory"/> are strings including the
    /// size (a <c>double</c>) along with an optional unit designation from
    /// <see cref="ByteUnits"/> like <b>KiB</b>, <b>MiB</b>, and <b>GiB</b> rather 
    /// than using the Docker unit conventions for consistency with neonKUBE
    /// cluster definitions, etc.  These values will be converted to a simple 
    /// byte count before passing them on to Docker.
    /// </note>
    /// <note>
    /// We're currently implementing some memory limits along with one CPU limit.
    /// </note>
    /// </summary>
    public class ContainerLimits
    {
        /// <summary>
        /// Optionally specifies the maximum memory that can be allocated to the
        /// container.  The minimum value is <b>4MiB</b>.  The default value is
        /// unconstrained.
        /// </summary>
        public string Memory { get; set; } = null;

        /// <summary>
        /// <para>
        /// The amount of memory the container is allowed to swap to disk.  This
        /// required <see cref="Memory"/> to be also set to have any effect.  See
        /// the Docker documentation for more details on how this works:
        /// </para>
        /// <para>
        /// <a href="https://docs.docker.com/config/containers/resource_constraints/#--memory-swap-details">https://docs.docker.com/config/containers/resource_constraints/#--memory-swap-details</a>
        /// </para>
        /// <para>
        /// 
        /// </para>
        /// </summary>
        public string MemorySwap { get; set; } = null;

        /// <summary>
        /// <para>
        /// The percentage of anonymous memory pages used by the container that may
        /// be swapped to disk.  This is an integer number between 0..100.  See the
        /// Docker documentation for more details on how this works:
        /// </para>
        /// <para>
        /// <a href="https://docs.docker.com/config/containers/resource_constraints/#--memory-swappiness-details">https://docs.docker.com/config/containers/resource_constraints/#--memory-swappiness-details</a>
        /// </para>
        /// </summary>
        public int? MemorySwappiness { get; set; } = null;

        /// <summary>
        /// <para>
        /// Specifies a lower soft limit on memory than <see cref="Memory"/> when Docker
        /// detects contention or low memory on the host.  <see cref="Memory"/> must also
        /// be set and <see cref="MemoryReservation"/> must be less than that for this
        /// to have any effect.
        /// </para>
        /// <note>
        /// Because it is a soft limit, it does not guarantee that the container doesn’t
        /// exceed this value.
        /// </note>
        /// </summary>
        public string MemoryReservation { get; set; } = null;

        /// <summary>
        /// <para>
        /// The minimum amount of kernel memory the container can use.  Setting this can
        /// prevent the container from obtaining so much kernel memory that other machine
        /// level components are impacted.  The minimum value is <b>4MiB</b> and the default
        /// is unconstrained.  See the Docker documentation for more details on how this works:
        /// </para>
        /// <para>
        /// <a href="https://docs.docker.com/config/containers/resource_constraints/#--kernel-memory-details">https://docs.docker.com/config/containers/resource_constraints/#--kernel-memory-details</a>
        /// </para>
        /// </summary>
        public string KernelMemory { get; set; } = null;

        /// <summary>
        /// <para>
        /// By default, the host machine's OOM killer will kill processes in a container
        /// when the host runs out of memory (OOM).  You can disable this behavior by setting
        /// this to <c>true</c>.  This defaults to <c>false</c>.
        /// </para>
        /// <note>
        /// <para>
        /// **WARNING:** You should also set <see cref="Memory"/> when enabling this to 
        /// help prevent the OOM killer from killing important host level processes.
        /// </para>
        /// <para>
        /// <see cref="Validate"/> will check for this condition.
        /// </para>
        /// </note>
        /// </summary>
        public bool OomKillDisable { get; set; } = false;

        /// <summary>
        /// Verifies that the limit properties make sense.
        /// </summary>
        /// <returns><c>null</c> for valid properties, otherwise an error message.</returns>
        public string Validate()
        {
            decimal memory            = -1;
            decimal memorySwap        = -1;
            decimal memoryReservation = -1;
            decimal kernelMemory      = -1;

            if (Memory != null)
            {
                try
                {
                    memory = ByteUnits.Parse(Memory);

                    if (memory < 0)
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    return $"[{nameof(Memory)}={Memory}]: Value is not valid.";
                }
            }

            if (MemorySwap != null)
            {
                try
                {
                    memorySwap = ByteUnits.Parse(MemorySwap);

                    if (memorySwap < -1)
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    return $"[{nameof(MemorySwap)}={MemorySwap}]: Value is not valid.";
                }

                if (memorySwap > 0 && memory == -1)
                {
                    return $"[{nameof(Memory)}] must also be set when [{nameof(MemorySwap)}] is specified.";
                }

                if (memory != -1 && memorySwap <= memory)
                {
                    return $"[{nameof(MemorySwap)}={MemorySwap}] must be greater than [{nameof(Memory)}={Memory}].";
                }
            }

            if (MemorySwappiness != null)
            {
                if (MemorySwappiness.Value < 0 || MemorySwappiness.Value > 100)
                {
                    return $"[{nameof(MemorySwappiness)}={MemorySwappiness}]: Value is not valid.";
                }
            }

            if (MemoryReservation != null)
            {
                try
                {
                    memoryReservation = ByteUnits.Parse(MemoryReservation);

                    if (memoryReservation < 0)
                    {
                        throw new Exception();
                    }
                }
                catch
                {
                    return $"[{nameof(MemoryReservation)}={MemoryReservation}]: Value is not valid.";
                }
            }

            if (KernelMemory != null)
            {
                try
                {
                    kernelMemory = ByteUnits.Parse(KernelMemory);

                    if (memory < 4 * ByteUnits.MebiBytes)
                    {
                        return $"[{nameof(Memory)}={Memory}]: Value cannot be less than 4MiB.";
                    }
                }
                catch
                {
                    return $"[{nameof(KernelMemory)}={KernelMemory}]: Value is not valid.";
                }

                if (kernelMemory < 4 * ByteUnits.MebiBytes)
                {
                    return $"[{nameof(KernelMemory)}={KernelMemory}]: Value cannot be less than 4MiB.";
                }
            }

            if (memory != -1 && memoryReservation != -1)
            {
                if (memoryReservation >= memory)
                {
                    return $"[{nameof(MemoryReservation)}={MemoryReservation}] must be less than [{nameof(Memory)}={Memory}].";
                }
            }

            if (OomKillDisable && memory == -1)
            {
                return $"[{nameof(OomKillDisable)}={OomKillDisable}] is not allowed when [{nameof(Memory)}] is not set.";
            }

            return null;
        }
    }
}

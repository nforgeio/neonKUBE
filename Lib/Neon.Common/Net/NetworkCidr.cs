//-----------------------------------------------------------------------------
// FILE:	    NetworkCidr.cs
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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

using Neon.Common;
using Neon.Collections;

// $todo(jeff.lill): Support IPv6.

namespace Neon.Net
{
    /// <summary>
    /// Describes a IP network subnet using Classless Inter-Domain Routing (CIDR) notation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is generally used for describing an IP subnet.  See the following Wikipedia
    /// article for more information.
    /// </para>
    /// <para>
    /// https://en.wikipedia.org/wiki/Classless_Inter-Domain_Routing#CIDR_notation
    /// </para>
    /// <note>
    /// This class currently supports only IPv4 addresses.
    /// </note>
    /// </remarks>
    public class NetworkCidr
    {
        //---------------------------------------------------------------------
        // Operators

        /// <summary>
        /// Compares two <see cref="NetworkCidr"/> instances for equality.
        /// </summary>
        /// <param name="v1">Value 1.</param>
        /// <param name="v2">Value 2</param>
        /// <returns><c>true</c> if the values are equal.</returns>
        public static bool operator ==(NetworkCidr v1, NetworkCidr v2)
        {
            var v1Null = object.ReferenceEquals(v1, null);
            var v2Null = object.ReferenceEquals(v2, null);

            if (v1Null && v2Null)
            {
                return true;
            }
            else if (v1Null && !v2Null)
            {
                return false;
            }
            else if (!v1Null && v2Null)
            {
                return false;
            }

            return v1.Equals(v2);
        }

        /// <summary>
        /// Compares two <see cref="NetworkCidr"/> instances for inequality.
        /// </summary>
        /// <param name="v1">Value 1.</param>
        /// <param name="v2">Value 2</param>
        /// <returns><c>true</c> if the values are not equal.</returns>
        public static bool operator !=(NetworkCidr v1, NetworkCidr v2)
        {
            return !(v1 == v2);
        }

        /// <summary>
        /// Implicitly casts a <see cref="NetworkCidr"/> into a string.
        /// </summary>
        /// <param name="v">The value (or <c>null)</c>.</param>
        public static implicit operator string(NetworkCidr v)
        {
            if (v == null)
            {
                return null;
            }

            return v.ToString();
        }

        //---------------------------------------------------------------------
        // Static members

        /// <summary>
        /// Returns the <b>0.0.0.0/0</b> subnet which includes all public and private
        /// IP addresses.
        /// </summary>
        public static NetworkCidr All { get; private set; } = new NetworkCidr(new IPAddress(new byte[] { 0, 0, 0, 0 }), 0);

        /// <summary>
        /// Parses a subnet from CIDR notation in the form of <i>ip-address</i>/<i>prefix</i>,
        /// where <i>prefix</i> is the network prefix length in bits.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The parsed <see cref="NetworkCidr"/>.</returns>
        /// <exception cref="ArgumentException">Thrown if the input is not correctly formatted.</exception>
        public static NetworkCidr Parse(string input)
        {
            Covenant.Requires<ArgumentNullException>(!string.IsNullOrEmpty(input));

            var slashPos = input.IndexOf('/');

            if (slashPos <= 0)
            {
                throw new ArgumentException($"Invalid CIDR [{input}].");
            }

            if (!IPAddress.TryParse(input.Substring(0, slashPos), out var address))
            {
                throw new ArgumentException($"Invalid CIDR [{input}].");
            }

            if (!int.TryParse(input.Substring(slashPos + 1), out var prefixLength) || prefixLength < 0 || prefixLength > 32)
            {
                throw new ArgumentException($"Invalid CIDR [{input}].");
            }

            var cidr = new NetworkCidr();

            cidr.Initialize(address, prefixLength);

            return cidr;
        }

        /// <summary>
        /// Attempts to parse a subnet from CIDR notation in the form of <i>ip-address</i>/<i>prefix</i>,
        /// where <i>prefix</i> is the network prefix length in bits.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <param name="cidr">The parsed <see cref="NetworkCidr"/>.</param>
        /// <returns><c>true</c> if the operation was successful.</returns>
        public static bool TryParse(string input, out NetworkCidr cidr)
        {
            cidr = null;

            if (string.IsNullOrEmpty(input))
            {
                return false;
            }

            var slashPos = input.IndexOf('/');

            if (slashPos <= 0)
            {
                return false;
            }

            if (!IPAddress.TryParse(input.Substring(0, slashPos), out var address))
            {
                return false;
            }

            if (!int.TryParse(input.Substring(slashPos + 1), out var prefixLength) || prefixLength < 0 || prefixLength > 32)
            {
                return false;
            }

            cidr = new NetworkCidr();

            cidr.Initialize(address, prefixLength);

            return true;
        }

        /// <summary>
        /// Attempts to normalize a network CIDR string by ensuring that the
        /// address actually fits the mask.
        /// </summary>
        /// <param name="input">The input string.</param>
        /// <returns>The normalizes CIDR converted back to a string.</returns>
        /// <exception cref="ArgumentException">Thrown if the input is not a valid CIDR.</exception>
        public static string Normalize(string input)
        {
            if (!TryParse(input, out var cidr))
            {
                throw new ArgumentException(nameof(input));
            }

            return cidr.ToString();
        }

        //---------------------------------------------------------------------
        // Instance members

        /// <summary>
        /// Private constructor.
        /// </summary>
        private NetworkCidr()
        {
        }

        /// <summary>
        /// Creates a subnet from an IP address and prefix length.
        /// </summary>
        /// <param name="address">The IP address.</param>
        /// <param name="prefixLength">The network prefix mask length in bits.</param>
        public NetworkCidr(IPAddress address, int prefixLength)
        {
            Covenant.Requires<ArgumentNullException>(address != null);
            Covenant.Requires<ArgumentException>(address.AddressFamily == AddressFamily.InterNetwork);
            Covenant.Requires<ArgumentException>(0 <= prefixLength && prefixLength <= 32);

            Initialize(address, prefixLength);
        }

        /// <summary>
        /// Initializes the instance.
        /// </summary>
        /// <param name="address">The IP address.</param>
        /// <param name="prefixLength">The network prefix mask length in bits.</param>
        private void Initialize(IPAddress address, int prefixLength)
        {
            PrefixLength = prefixLength;

            var maskBits  = new Bits(32);

            for (int i = 0; i < prefixLength; i++)
            {
                maskBits[i] = true;
            }

            Mask    = new IPAddress(maskBits.ToBytes());
            Address = NetHelper.UintToAddress(NetHelper.AddressToUint(address) & NetHelper.AddressToUint(Mask));
        }

        /// <summary>
        /// Returns the CIDR address.
        /// </summary>
        public IPAddress Address { get; private set; }

        /// <summary>
        /// Returns the subnet mask.
        /// </summary>
        public IPAddress Mask { get; private set; }

        /// <summary>
        /// Returns the prefix length in bits.
        /// </summary>
        public int PrefixLength { get; private set; }

        /// <summary>
        /// Returns the number of IP addresses within the subnet.
        /// </summary>
        public long AddressCount
        {
            get { return 1L << (31 - PrefixLength) + 1; }
        }

        /// <summary>
        /// Returns the first IP address in the subnet.
        /// </summary>
        public IPAddress FirstAddress
        {
            get
            {
                var addressBytes = Address.GetAddressBytes();
                var maskBytes    = Mask.GetAddressBytes();

                addressBytes[0] &= maskBytes[0];
                addressBytes[1] &= maskBytes[1];
                addressBytes[2] &= maskBytes[2];
                addressBytes[3] &= maskBytes[3];

                return new IPAddress(addressBytes);
            }
        }

        /// <summary>
        /// Returns the first usable IP address in the subnet.
        /// </summary>
        public IPAddress FirstUsableAddress
        {
            get { return NetHelper.AddressIncrement(FirstAddress); }
        }

        /// <summary>
        /// Returns the last IP address in the subnet.
        /// </summary>
        public IPAddress LastAddress
        {
            get
            {
                if (AddressCount > int.MaxValue)
                {
                    return new IPAddress(new byte[] { 255, 255, 255, 255 });
                }
                else
                {
                    return NetHelper.AddressIncrement(FirstAddress, (int)AddressCount - 1);
                }
            }
        }

        /// <summary>
        /// Returns the first address after the subnet.
        /// </summary>
        public IPAddress NextAddress
        {
            get
            {
                if (AddressCount > int.MaxValue)
                {
                    return new IPAddress(new byte[] { 255, 255, 255, 255 });
                }
                else
                {
                    return NetHelper.AddressIncrement(FirstAddress, (int)AddressCount);
                }
            }
        }

        /// <summary>
        /// Determines whether an IP address is within the subnet.
        /// </summary>
        /// <param name="address">The IP address.</param>
        /// <returns><c>true</c> if the address is within the subnet.</returns>
        /// <exception cref="NotSupportedException">Thrown if for IPv6 addresses.</exception>
        public bool Contains(IPAddress address)
        {
            if (address.AddressFamily != AddressFamily.InterNetwork)
            {
                throw new NotSupportedException("Only IPv4 addresses are supported.");
            }

            var maskBits   = new Bits(Mask.GetAddressBytes());
            var subnetBits = new Bits(Address.GetAddressBytes());

            subnetBits = subnetBits.And(maskBits);      // Zero any address bits to the right of the prefix

            var addressBits = new Bits(address.GetAddressBytes());

            addressBits = addressBits.And(maskBits);    // Zero any address bits to the right of the prefix

            for (int i = 0; i < addressBits.Length; i++)
            {
                if (addressBits[i] != subnetBits[i])
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether a subnet is completely contained within the current subnet.
        /// </summary>
        /// <param name="subnet">The subnet being tested.</param>
        /// <returns><c>true</c> if <paramref name="subnet"/> is fully contained.</returns>
        public bool Contains(NetworkCidr subnet)
        {
            Covenant.Requires<ArgumentNullException>(subnet != null);

            return Contains(subnet.FirstAddress) && Contains(subnet.LastAddress);
        }

        /// <summary>
        /// Determines whether this subnet overlaps another.
        /// </summary>
        /// <param name="subnet">The subnet being tested.</param>
        /// <returns><c>true</c> if the subnets overlap.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="subnet"/> is <c>null</c>.</exception>
        public bool Overlaps(NetworkCidr subnet)
        {
            Covenant.Requires<ArgumentNullException>(subnet != null);

            var first       = NetHelper.AddressToUint(this.FirstAddress);
            var last        = NetHelper.AddressToUint(this.LastAddress);
            var subnetFirst = NetHelper.AddressToUint(subnet.FirstAddress);
            var subnetLast  = NetHelper.AddressToUint(subnet.LastAddress);

            if (last < subnetFirst)
            {
                return false;
            }
            else if (first > subnetLast)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return $"{Address}/{PrefixLength}";
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Address.GetHashCode() ^ PrefixLength.GetHashCode();
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            if (!(obj is NetworkCidr))
            {
                return false;
            }

            var other = (NetworkCidr)obj;

            return other.Address.Equals(this.Address) && other.PrefixLength == this.PrefixLength;
        }
    }
}

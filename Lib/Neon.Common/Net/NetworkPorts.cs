//-----------------------------------------------------------------------------
// FILE:	    NetworkPorts.cs
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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Neon.Net
{
    /// <summary>
    /// Defines some common network port numbers as well as the <see cref="TryParse" /> method.
    /// </summary>
    public static class NetworkPorts
    {
        /// <summary>
        /// HyperText Transport Protocol port (<b>80</b>).
        /// </summary>
        public const int HTTP = 80;

        /// <summary>
        /// Secure HyperText Transport Protocol port (<b>443</b>).
        /// </summary>
        public const int HTTPS = 443;

        /// <summary>
        /// Secure Socket Layer port (<b>443</b>).
        /// </summary>
        public const int SSL = 443;

        /// <summary>
        /// Domain Name System port (<b>53</b>).
        /// </summary>
        public const int DNS = 53;

        /// <summary>
        /// Simple Message Transport Protocol port (<b>25</b>).
        /// </summary>
        public const int SMTP = 25;

        /// <summary>
        /// Post Office Protocol version 3 port (<b>110</b>).
        /// </summary>
        public const int POP3 = 110;

        /// <summary>
        /// Remote terminal protocol port (<b>23</b>).
        /// </summary>
        public const int TELNET = 23;

        /// <summary>
        /// File Transfer Protocol (control) port (<b>21</b>).
        /// </summary>
        public const int FTP = 21;

        /// <summary>
        /// File Transfer Protocol (data) port (<b>20</b>).
        /// </summary>
        public const int FTPDATA = 20;

        /// <summary>
        /// Secure File Transfer Protocol port (<b>22</b>).
        /// </summary>
        public const int SFTP = 22;

        /// <summary>
        /// RADIUS authentication and billing protocol (port <b>1812</b>).
        /// </summary>
        public const int RADIUS = 1812;

        /// <summary>
        /// Authentication, Authorization, and Accounting port.  This port was
        /// originally used by the RADIUS protocol and is still used
        /// fairly widely (<b>1645</b>).
        /// </summary>
        public const int AAA = 1645;

        /// <summary>
        /// PING port (<b>7</b>).
        /// </summary>
        public const int ECHO = 7;

        /// <summary>
        /// Daytime (RFC 867) port (<b>13</b>).
        /// </summary>
        public const int DAYTIME = 13;

        /// <summary>
        /// Trivial File Transfer Protocol port (<b>69</b>).
        /// </summary>
        public const int TFTP = 69;

        /// <summary>
        /// Secure Shell port (<b>22</b>).
        /// </summary>
        public const int SSH = 22;

        /// <summary>
        /// TIME protocol port (<b>37</b>).
        /// </summary>
        public const int TIME = 37;

        /// <summary>
        /// Network Time Protocol port (<b>123</b>).
        /// </summary>
        public const int NTP = 123;

        /// <summary>
        /// Internet Message Access Protocol port (<b>143</b>).
        /// </summary>
        public const int IMAP = 143;

        /// <summary>
        /// Simple Network Managenment Protocol (SNMP) port (<b>161</b>).
        /// </summary>
        public const int SNMP = 161;

        /// <summary>
        /// Simple Network Managenment Protocol (trap) port (<b>162</b>)
        /// </summary>
        public const int SNMPTRAP = 162;

        /// <summary>
        /// Lightweight Directory Access Protocol port (<b>389</b>).
        /// </summary>
        public const int LDAP = 389;

        /// <summary>
        /// Lightweight Directory Access Protocol over TLS/SSL port (<b>636</b>).
        /// </summary>
        public const int LDAPS = 636;

        /// <summary>
        /// Session Initiation Protocol port (<b>5060</b>).
        /// </summary>
        public const int SIP = 5060;

        /// <summary>
        /// Secure Session Initiation Protocol (over TLS) port (<b>5061</b>).
        /// </summary>
        public const int SIPS = 5061;

        /// <summary>
        /// The default port for the <a href="http://en.wikipedia.org/wiki/Squid_%28software%29">Squid</a>
        /// open source proxy project port (<b>3128</b>).
        /// </summary>
        public const int SQUID = 3128;

        /// <summary>
        /// The SOCKS (Socket Secure) proxy port (<b>1080</b>).
        /// </summary>
        public const int SOCKS = 1080;

        /// <summary>
        /// The HashiCorp Consul service (RPC) port (<b>8500</b>).  The protocol
        /// will be HTTP or HTTPS depending on how Consul is configured.
        /// </summary>
        public const int Consul = 8500;

        /// <summary>
        /// The HashiCorp Vault service port (<b>8200</b>).
        /// </summary>
        public const int Vault = 8200;

        /// <summary>
        /// The Docker API port (<b>2375</b>).
        /// </summary>
        public const int Docker = 2375;

        /// <summary>
        /// The Docker Swarm node advertise port (<b>2377</b>).
        /// </summary>
        public const int DockerSwarm = 2377;

        /// <summary>
        /// The Etcd API port (<b>2379</b>).
        /// </summary>
        public const int Etcd = 2379;

        /// <summary>
        /// The internal Etcd cluster peer API port (<b>2380</b>).
        /// </summary>
        public const int EtcdPeer = 2380;

        /// <summary>
        /// The Treasure Data <b>td-agent</b> <b>forward</b> port 
        /// to accept TCP and UDP traffic (<b>24224</b>).
        /// </summary>
        public const int TDAgentForward = 24224;

        /// <summary>
        /// The Treasure Data <b>td-agent</b> <b>HTTP</b> port (<b>9880</b>).
        /// </summary>
        public const int TDAgentHttp = 9880;

        /// <summary>
        /// The ElasticSearch client HTTP port (<b>9200</b>).
        /// </summary>
        public const int ElasticSearchHttp = 9200;

        /// <summary>
        /// The ElasticSearch client TCP port (<b>9300</b>).
        /// </summary>
        public const int ElasticSearchTcp = 9300;

        /// <summary>
        /// The Kibana website port (<b>5601</b>).
        /// </summary>
        public const int Kibana = 5601;

        /// <summary>
        /// The SysLog UDP port (<b>514</b>).
        /// </summary>
        public const int SysLog = 514;

        /// <summary>
        /// The Couchbase Server web administration user interface port (<b>8091</b>).
        /// </summary>
        public const int CouchbaseWebAdmin = 8091;

        /// <summary>
        /// The Couchbase Server REST API port (<b>8092</b>).
        /// </summary>
        public const int CouchbaseApi = 8092;

        /// <summary>
        /// The Couchbase Sync Gateway administration REST API port (<b>4985</b>).
        /// </summary>
        public const int CouchbaseSyncGatewayAdmin = 4985;

        /// <summary>
        /// The Couchbase Sync Gateway public REST API port (<b>4984</b>).
        /// </summary>
        public const int CouchbaseSyncGatewayPublic = 4984;

        /// <summary>
        /// The OpenVPN port.
        /// </summary>
        public const int OpenVPN = 1194;

        /// <summary>
        /// The Advanced Messaging Queue Protocol (AMQP) port (e.g. RabbitMQ).
        /// </summary>
        public const int AMQP = 5672;

        /// <summary>
        /// RabbitMQ Admin dashboard port.
        /// </summary>
        public const int RabbitMQAdmin = 15672;

        /// <summary>
        /// <b>apt-cacher-ng</b> Debian/Ubuntu package proxy port.
        /// </summary>
        public const int AppCacherNg = 3142;

        /// <summary>
        /// Uber Cadence primary cluster port.
        /// </summary>
        public const int Cadence = 7933;

        private static Dictionary<string, int> wellKnownMap;

        private struct Map
        {
            public string Name;
            public int Port;

            public Map(string name, int Port)
            {
                this.Name = name;
                this.Port = Port;
            }
        }

        static NetworkPorts()
        {
            // Initialize the well known port map.

            var ports = new Map[] {

                new Map("ANY", 0),
                new Map("HTTP", HTTP),
                new Map("HTTPS", HTTPS),
                new Map("SSL", SSL),
                new Map("DNS", DNS),
                new Map("SMTP", SMTP),
                new Map("POP3", POP3),
                new Map("TELNET", TELNET),
                new Map("FTP", FTP),
                new Map("FTPDATA", FTPDATA),
                new Map("SFTP", SFTP),
                new Map("RADIUS", RADIUS),
                new Map("AAA", AAA),
                new Map("ECHO", ECHO),
                new Map("DAYTIME", DAYTIME),
                new Map("TFTP", TFTP),
                new Map("SSH", SSH),
                new Map("TIME", TIME),
                new Map("NTP", NTP),
                new Map("IMAP", IMAP),
                new Map("SNMP", SNMP),
                new Map("SNMTRAP", SNMPTRAP),
                new Map("LDAP", LDAP),
                new Map("LDAPS", LDAPS),
                new Map("SIP", SIP),
                new Map("SIPS", SIPS),
                new Map("SQUID", SQUID),
                new Map("SOCKS", SOCKS),
                new Map("Consul", Consul),
                new Map("Vault", Vault),
                new Map("Docker", Docker),
                new Map("DockerSwarm", DockerSwarm),
                new Map("Etcd", Etcd),
                new Map("EtcdPeer", EtcdPeer),
                new Map("TDAgentForward", TDAgentForward),
                new Map("TDAgentHttp", TDAgentHttp),
                new Map("ElasticSearchHttp", ElasticSearchHttp),
                new Map("ElasticSearchTcp", ElasticSearchTcp),
                new Map("Kibana", Kibana),
                new Map("SysLog", SysLog),
                new Map("CouchbaseWebAdmin", CouchbaseWebAdmin),
                new Map("CouchbaseApi", CouchbaseApi),
                new Map("CouchbaseSyncGatewayAdmin", CouchbaseSyncGatewayAdmin),
                new Map("CouchbaseSyncGatewayPublic", CouchbaseSyncGatewayPublic),
                new Map("OpenVPN", OpenVPN),
                new Map("AMQP", AMQP),
                new Map("RabbitMQAdmin", RabbitMQAdmin),
                new Map("aptcacherng", AppCacherNg),
                new Map("cadence", Cadence)
            };

        wellKnownMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (Map map in ports)
            {
                wellKnownMap.Add(map.Name, map.Port);
            }
        }

        /// <summary>
        /// Attempts to parse an integer or well known port name from a string
        /// and return the integer TCP port number.
        /// </summary>
        /// <param name="input">The port number or name as as string.</param>
        /// <param name="port">Receives the parsed port number.</param>
        /// <returns><c>true</c> if a port was successfulyy parsed.</returns>
        public static bool TryParse(string input, out int port)
        {
            port  = 0;
            input = input.Trim();

            if (int.TryParse(input, out port))
            {
                return true;
            }

            return wellKnownMap.TryGetValue(input, out port);
        }
    }
}

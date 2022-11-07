#!/bin/bash
#------------------------------------------------------------------------------
# FILE:         net-interface
# CONTRIBUTOR:  Jeff Lill
# COPYRIGHT:    Copyright © 2005-2022 by NEONFORGE LLC.  All rights reserved.
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#     http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.
#
# Scans the network interfaces via [ip address] to idenfify the primary
# network interface.
#
# USAGE:
#
#   net-interface
#
# REMARKS:
#
# This is pretty simplistic.  It simply returns the name of the first non-loopback
# interface to standard output.

# So the [ip address] command returns output like:
#
#    1: lo: <LOOPBACK,UP,LOWER_UP> mtu 65536 qdisc noqueue state UNKNOWN group default qlen 1000
#        link/loopback 00:00:00:00:00:00 brd 00:00:00:00:00:00
#        inet 127.0.0.1/8 scope host lo
#           valid_lft forever preferred_lft forever
#        inet6 ::1/128 scope host 
#           valid_lft forever preferred_lft forever
#    2: ens5: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 9001 qdisc mq state UP group default qlen 1000
#        link/ether 0e:1e:96:23:01:25 brd ff:ff:ff:ff:ff:ff
#        altname enp0s5
#        inet 172.31.49.107/20 brd 172.31.63.255 scope global dynamic ens5
#           valid_lft 2654sec preferred_lft 2654sec
#        inet6 fe80::c1e:96ff:fe23:125/64 scope link 
#           valid_lft forever preferred_lft forever
#
# We're going to pipe together some Linux commands to return the name of the first
# non-loopback interface, "ens5" in this case.
#
#   1. Execute [ip address] to get interface information
#   2. [grep] for the lines with interface numbers and names
#   3. [cut] the result at the colons (:) and extract second field with the interface name
#   4. [tr] removes embedded spaces
#   5. [grep] to remove any names starting with "lo"
#   6. [head] to take just the first lines
#   7. [tr] to remove the line feed
#
# NOTE: It's amazing how difficult operations like this are in stock Linux.
#       Why doesn't any of the grep variants have the ability to output a
#       capture group???  Seems like something we should have in 2022!

ip address | grep '^.*: .*:' | cut -d ':' -f 2 | tr -d ' ' | grep '^lo' -v | head -1 | tr -d '\n'

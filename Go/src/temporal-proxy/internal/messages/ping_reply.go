//-----------------------------------------------------------------------------
// FILE:		ping_reply.go
// CONTRIBUTOR: John C Burns
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

package messages

import (
	internal "temporal-proxy/internal"
)

type (

	// PingReply is a ProxyReply of MessageType
	// PingReply It holds a reference to a
	// ProxyReply in memory
	PingReply struct {
		*ProxyReply
	}
)

// NewPingReply is the default constructor for
// a PingReply
//
// returns *PingReply -> pointer to a newly initialized
// PingReply in memory
func NewPingReply() *PingReply {
	reply := new(PingReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(internal.PingReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *PingReply) Clone() IProxyMessage {
	pingReply := NewPingReply()
	var messageClone IProxyMessage = pingReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *PingReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

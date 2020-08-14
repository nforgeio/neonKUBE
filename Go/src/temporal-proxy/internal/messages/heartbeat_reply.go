//-----------------------------------------------------------------------------
// FILE:		heartbeat_reply.go
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

	// HeartbeatReply is a ProxyReply of MessageType
	// HeartbeatReply It holds a reference to a
	// ProxyReply in memory
	HeartbeatReply struct {
		*ProxyReply
	}
)

// NewHeartbeatReply is the default constructor for
// a HeartbeatReply
//
// returns *HeartbeatReply -> pointer to a newly initialized
// HeartbeatReply in memory
func NewHeartbeatReply() *HeartbeatReply {
	reply := new(HeartbeatReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(internal.HeartbeatReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from ProxyReply.Build()
func (reply *HeartbeatReply) Build(e error, result ...interface{}) {
	reply.ProxyReply.Build(e)
}

// Clone inherits docs from ProxyReply.Clone()
func (reply *HeartbeatReply) Clone() IProxyMessage {
	heartbeatReply := NewHeartbeatReply()
	var messageClone IProxyMessage = heartbeatReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *HeartbeatReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

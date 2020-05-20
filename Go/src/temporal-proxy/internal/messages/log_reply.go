//-----------------------------------------------------------------------------
// FILE:		log_reply.go
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
	proxyerror "temporal-proxy/internal/temporal/error"
)

type (

	// LogReply is a ProxyReply of MessageType
	// LogReply.  It holds a reference to a ProxyReply in memory
	LogReply struct {
		*ProxyReply
	}
)

// NewLogReply is the default constructor for
// a LogReply
//
// returns *LogReply -> a pointer to a newly initialized
// LogReply in memory
func NewLogReply() *LogReply {
	reply := new(LogReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(internal.LogReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from ProxyReply.Build()
func (reply *LogReply) Build(e *proxyerror.TemporalError, result ...interface{}) {
	reply.ProxyReply.Build(e)
}

// Clone inherits docs from ProxyReply.Clone()
func (reply *LogReply) Clone() IProxyMessage {
	logReply := NewLogReply()
	var messageClone IProxyMessage = logReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *LogReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

//-----------------------------------------------------------------------------
// FILE:		new_worker_reply.go
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

	// NewWorkerReply is a ProxyReply of MessageType
	// NewWorkerReply.  It holds a reference to a ProxyReply in memory
	NewWorkerReply struct {
		*ProxyReply
	}
)

// NewNewWorkerReply is the default constructor for
// a NewWorkerReply
//
// returns *NewWorkerReply -> a pointer to a newly initialized
// NewWorkerReply in memory
func NewNewWorkerReply() *NewWorkerReply {
	reply := new(NewWorkerReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(internal.NewWorkerReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from ProxyReply.Build()
func (reply *NewWorkerReply) Build(e error, result ...interface{}) {
	reply.ProxyReply.Build(e)
	if len(result) > 0 {
		if v, ok := result[0].(int64); ok {
			reply.SetWorkerID(v)
		}
	}
}

// Clone inherits docs from ProxyReply.Clone()
func (reply *NewWorkerReply) Clone() IProxyMessage {
	newWorkerReply := NewNewWorkerReply()
	var messageClone IProxyMessage = newWorkerReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *NewWorkerReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

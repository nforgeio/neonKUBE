//-----------------------------------------------------------------------------
// FILE:		namespace_update_reply.go
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

	// NamespaceUpdateReply is a ProxyReply of MessageType
	// NamespaceUpdateReply.  It holds a reference to a ProxyReply in memory
	NamespaceUpdateReply struct {
		*ProxyReply
	}
)

// NewNamespaceUpdateReply is the default constructor for
// a NamespaceUpdateReply
//
// returns *NamespaceUpdateReply -> a pointer to a newly initialized
// NamespaceUpdateReply in memory
func NewNamespaceUpdateReply() *NamespaceUpdateReply {
	reply := new(NamespaceUpdateReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(internal.NamespaceUpdateReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from ProxyReply.Build()
func (reply *NamespaceUpdateReply) Build(e error, result ...interface{}) {
	reply.ProxyReply.Build(e)
}

// Clone inherits docs from ProxyReply.Clone()
func (reply *NamespaceUpdateReply) Clone() IProxyMessage {
	namespaceUpdateReply := NewNamespaceUpdateReply()
	var messageClone IProxyMessage = namespaceUpdateReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *NamespaceUpdateReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

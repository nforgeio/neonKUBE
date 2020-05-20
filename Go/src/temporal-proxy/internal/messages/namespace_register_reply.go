//-----------------------------------------------------------------------------
// FILE:		namespace_register_reply.go
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

	// NamespaceRegisterReply is a ProxyReply of MessageType
	// NamespaceRegisterReply.  It holds a reference to a ProxyReply in memory
	NamespaceRegisterReply struct {
		*ProxyReply
	}
)

// NewNamespaceRegisterReply is the default constructor for
// a NamespaceRegisterReply
//
// returns *NamespaceRegisterReply -> a pointer to a newly initialized
// NamespaceRegisterReply in memory
func NewNamespaceRegisterReply() *NamespaceRegisterReply {
	reply := new(NamespaceRegisterReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(internal.NamespaceRegisterReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from ProxyReply.Build()
func (reply *NamespaceRegisterReply) Build(e *proxyerror.TemporalError, result ...interface{}) {
	reply.ProxyReply.Build(e)
}

// Clone inherits docs from ProxyReply.Clone()
func (reply *NamespaceRegisterReply) Clone() IProxyMessage {
	namespaceRegisterReply := NewNamespaceRegisterReply()
	var messageClone IProxyMessage = namespaceRegisterReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *NamespaceRegisterReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

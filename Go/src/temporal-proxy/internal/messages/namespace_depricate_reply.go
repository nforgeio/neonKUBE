//-----------------------------------------------------------------------------
// FILE:		namespace_deprecate_reply.go
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

	// NamespaceDeprecateReply is a ProxyReply of MessageType
	// NamespaceDeprecateReply.  It holds a reference to a ProxyReply in memory
	NamespaceDeprecateReply struct {
		*ProxyReply
	}
)

// NewNamespaceDeprecateReply is the default constructor for
// a NamespaceDeprecateReply
//
// returns *NamespaceDeprecateReply -> a pointer to a newly initialized
// NamespaceDeprecateReply in memory
func NewNamespaceDeprecateReply() *NamespaceDeprecateReply {
	reply := new(NamespaceDeprecateReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(internal.NamespaceDeprecateReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from ProxyReply.Build()
func (reply *NamespaceDeprecateReply) Build(e *proxyerror.TemporalError, result ...interface{}) {
	reply.ProxyReply.Build(e)
}

// Clone inherits docs from ProxyReply.Clone()
func (reply *NamespaceDeprecateReply) Clone() IProxyMessage {
	namespaceDeprecateReply := NewNamespaceDeprecateReply()
	var messageClone IProxyMessage = namespaceDeprecateReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *NamespaceDeprecateReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

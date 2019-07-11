//-----------------------------------------------------------------------------
// FILE:		domain_deprecate_reply.go
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
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

type (

	// DomainDeprecateReply is a ProxyReply of MessageType
	// DomainDeprecateReply.  It holds a reference to a ProxyReply in memory
	DomainDeprecateReply struct {
		*ProxyReply
	}
)

// NewDomainDeprecateReply is the default constructor for
// a DomainDeprecateReply
//
// returns *DomainDeprecateReply -> a pointer to a newly initialized
// DomainDeprecateReply in memory
func NewDomainDeprecateReply() *DomainDeprecateReply {
	reply := new(DomainDeprecateReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(messagetypes.DomainDeprecateReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *DomainDeprecateReply) Clone() IProxyMessage {
	domainDeprecateReply := NewDomainDeprecateReply()
	var messageClone IProxyMessage = domainDeprecateReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *DomainDeprecateReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

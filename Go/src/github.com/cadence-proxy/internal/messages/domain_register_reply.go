//-----------------------------------------------------------------------------
// FILE:		domain_register_reply.go
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
	internal "github.com/cadence-proxy/internal"
)

type (

	// DomainRegisterReply is a ProxyReply of MessageType
	// DomainRegisterReply.  It holds a reference to a ProxyReply in memory
	DomainRegisterReply struct {
		*ProxyReply
	}
)

// NewDomainRegisterReply is the default constructor for
// a DomainRegisterReply
//
// returns *DomainRegisterReply -> a pointer to a newly initialized
// DomainRegisterReply in memory
func NewDomainRegisterReply() *DomainRegisterReply {
	reply := new(DomainRegisterReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(internal.DomainRegisterReply)

	return reply
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *DomainRegisterReply) Clone() IProxyMessage {
	domainRegisterReply := NewDomainRegisterReply()
	var messageClone IProxyMessage = domainRegisterReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *DomainRegisterReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
}

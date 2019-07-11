//-----------------------------------------------------------------------------
// FILE:		domain_deprecate_request.go
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

	// DomainDeprecateRequest is ProxyRequest of MessageType
	// DomainDeprecateRequest.
	//
	// A DomainDeprecateRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	DomainDeprecateRequest struct {
		*ProxyRequest
	}
)

// NewDomainDeprecateRequest is the default constructor for a DomainDeprecateRequest
//
// returns *DomainDeprecateRequest -> a reference to a newly initialized
// DomainDeprecateRequest in memory
func NewDomainDeprecateRequest() *DomainDeprecateRequest {
	request := new(DomainDeprecateRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(messagetypes.DomainDeprecateRequest)
	request.SetReplyType(messagetypes.DomainDeprecateReply)

	return request
}

// GetName gets a DomainDeprecateRequest's Name value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a DomainDeprecateRequest's Name
func (request *DomainDeprecateRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a DomainDeprecateRequest's Name value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *DomainDeprecateRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// GetSecurityToken gets a DomainDeprecateRequest's SecurityToken value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a DomainDeprecateRequest's SecurityToken
func (request *DomainDeprecateRequest) GetSecurityToken() *string {
	return request.GetStringProperty("SecurityToken")
}

// SetSecurityToken sets a DomainDeprecateRequest's SecurityToken value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *DomainDeprecateRequest) SetSecurityToken(value *string) {
	request.SetStringProperty("SecurityToken", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *DomainDeprecateRequest) Clone() IProxyMessage {
	domainDeprecateRequest := NewDomainDeprecateRequest()
	var messageClone IProxyMessage = domainDeprecateRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *DomainDeprecateRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*DomainDeprecateRequest); ok {
		v.SetName(request.GetName())
		v.SetSecurityToken(request.GetSecurityToken())
	}
}

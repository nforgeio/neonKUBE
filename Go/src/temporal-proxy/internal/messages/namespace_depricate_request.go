//-----------------------------------------------------------------------------
// FILE:		namespace_deprecate_request.go
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

	// NamespaceDeprecateRequest is ProxyRequest of MessageType
	// NamespaceDeprecateRequest.
	//
	// A NamespaceDeprecateRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	NamespaceDeprecateRequest struct {
		*ProxyRequest
	}
)

// NewNamespaceDeprecateRequest is the default constructor for a NamespaceDeprecateRequest
//
// returns *NamespaceDeprecateRequest -> a reference to a newly initialized
// NamespaceDeprecateRequest in memory
func NewNamespaceDeprecateRequest() *NamespaceDeprecateRequest {
	request := new(NamespaceDeprecateRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.NamespaceDeprecateRequest)
	request.SetReplyType(internal.NamespaceDeprecateReply)

	return request
}

// GetName gets a NamespaceDeprecateRequest's Name value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a NamespaceDeprecateRequest's Name
func (request *NamespaceDeprecateRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a NamespaceDeprecateRequest's Name value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *NamespaceDeprecateRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// GetSecurityToken gets a NamespaceDeprecateRequest's SecurityToken value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a NamespaceDeprecateRequest's SecurityToken
func (request *NamespaceDeprecateRequest) GetSecurityToken() *string {
	return request.GetStringProperty("SecurityToken")
}

// SetSecurityToken sets a NamespaceDeprecateRequest's SecurityToken value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *NamespaceDeprecateRequest) SetSecurityToken(value *string) {
	request.SetStringProperty("SecurityToken", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *NamespaceDeprecateRequest) Clone() IProxyMessage {
	namespaceDeprecateRequest := NewNamespaceDeprecateRequest()
	var messageClone IProxyMessage = namespaceDeprecateRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *NamespaceDeprecateRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*NamespaceDeprecateRequest); ok {
		v.SetName(request.GetName())
		v.SetSecurityToken(request.GetSecurityToken())
	}
}

//-----------------------------------------------------------------------------
// FILE:		namespace_describe_request.go
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

	// NamespaceDescribeRequest is ProxyRequest of MessageType
	// NamespaceDescribeRequest.
	//
	// A NamespaceDescribeRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	NamespaceDescribeRequest struct {
		*ProxyRequest
	}
)

// NewNamespaceDescribeRequest is the default constructor for a NamespaceDescribeRequest
//
// returns *NamespaceDescribeRequest -> a reference to a newly initialized
// NamespaceDescribeRequest in memory
func NewNamespaceDescribeRequest() *NamespaceDescribeRequest {
	request := new(NamespaceDescribeRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.NamespaceDescribeRequest)
	request.SetReplyType(internal.NamespaceDescribeReply)

	return request
}

// GetName gets a NamespaceDescribeRequest's Name value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a NamespaceDescribeRequest's Name
func (request *NamespaceDescribeRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a NamespaceDescribeRequest's TargetRequestId value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *NamespaceDescribeRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *NamespaceDescribeRequest) Clone() IProxyMessage {
	namespaceDescribeRequest := NewNamespaceDescribeRequest()
	var messageClone IProxyMessage = namespaceDescribeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *NamespaceDescribeRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*NamespaceDescribeRequest); ok {
		v.SetName(request.GetName())
	}
}

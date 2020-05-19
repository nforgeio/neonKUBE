//-----------------------------------------------------------------------------
// FILE:		domain_describe_request.go
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

	// DomainDescribeRequest is ProxyRequest of MessageType
	// DomainDescribeRequest.
	//
	// A DomainDescribeRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	DomainDescribeRequest struct {
		*ProxyRequest
	}
)

// NewDomainDescribeRequest is the default constructor for a DomainDescribeRequest
//
// returns *DomainDescribeRequest -> a reference to a newly initialized
// DomainDescribeRequest in memory
func NewDomainDescribeRequest() *DomainDescribeRequest {
	request := new(DomainDescribeRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.DomainDescribeRequest)
	request.SetReplyType(internal.DomainDescribeReply)

	return request
}

// GetName gets a DomainDescribeRequest's Name value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a DomainDescribeRequest's Name
func (request *DomainDescribeRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a DomainDescribeRequest's TargetRequestId value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *DomainDescribeRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *DomainDescribeRequest) Clone() IProxyMessage {
	domainDescribeRequest := NewDomainDescribeRequest()
	var messageClone IProxyMessage = domainDescribeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *DomainDescribeRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*DomainDescribeRequest); ok {
		v.SetName(request.GetName())
	}
}

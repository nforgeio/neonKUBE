//-----------------------------------------------------------------------------
// FILE:		cancel_request.go
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

	// CancelRequest is ProxyRequest of MessageType
	// CancelRequest.
	//
	// A CancelRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	CancelRequest struct {
		*ProxyRequest
	}
)

// NewCancelRequest is the default constructor for a CancelRequest
//
// returns *CancelRequest -> a reference to a newly initialized
// CancelRequest in memory
func NewCancelRequest() *CancelRequest {
	request := new(CancelRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.CancelRequest)
	request.SetReplyType(internal.CancelReply)

	return request
}

// GetTargetRequestID gets a CancelRequest's TargetRequestId value
// from its properties map
//
// returns int64 -> a long representing the target to cancels requestID that is
// in a CancelRequest's properties map
func (request *CancelRequest) GetTargetRequestID() int64 {
	return request.GetLongProperty("TargetRequestId")
}

// SetTargetRequestID sets a CancelRequest's TargetRequestId value
// in its properties map
//
// param value int64 -> a long value to be set in the properties map as a
// CancelRequest's TargetRequestId
func (request *CancelRequest) SetTargetRequestID(value int64) {
	request.SetLongProperty("TargetRequestId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *CancelRequest) Clone() IProxyMessage {
	cancelRequest := NewCancelRequest()
	var messageClone IProxyMessage = cancelRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *CancelRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*CancelRequest); ok {
		v.SetTargetRequestID(request.GetTargetRequestID())
	}
}

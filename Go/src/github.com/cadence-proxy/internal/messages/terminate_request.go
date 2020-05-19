//-----------------------------------------------------------------------------
// FILE:		terminate_request.go
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

	// TerminateRequest is ProxyRequest of MessageType
	// TerminateRequest.
	//
	// A TerminateRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	TerminateRequest struct {
		*ProxyRequest
	}
)

// NewTerminateRequest is the default constructor for
// TerminateRequest
//
// returns *TerminateRequest -> pointer to a newly initialized
// TerminateReqeuest in memory
func NewTerminateRequest() *TerminateRequest {
	request := new(TerminateRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.TerminateRequest)
	request.SetReplyType(internal.TerminateReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *TerminateRequest) Clone() IProxyMessage {
	terminateRequest := NewTerminateRequest()
	var messageClone IProxyMessage = terminateRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *TerminateRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
}

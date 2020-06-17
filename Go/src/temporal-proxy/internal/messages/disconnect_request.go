//-----------------------------------------------------------------------------
// FILE:		disconnect_request.go
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

	// DisconnectRequest is ProxyRequest of MessageType
	// DisconnectRequest.
	//
	// A DisconnectRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	DisconnectRequest struct {
		*ProxyRequest
	}
)

// NewDisconnectRequest is the default constructor for a DisconnectRequest
//
// returns *DisconnectRequest -> a reference to a newly initialized
// DisconnectRequest in memory
func NewDisconnectRequest() *DisconnectRequest {
	request := new(DisconnectRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.DisconnectRequest)
	request.SetReplyType(internal.DisconnectReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *DisconnectRequest) Clone() IProxyMessage {
	cancelRequest := NewDisconnectRequest()
	var messageClone IProxyMessage = cancelRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *DisconnectRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
}

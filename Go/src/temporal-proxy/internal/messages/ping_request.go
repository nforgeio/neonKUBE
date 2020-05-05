//-----------------------------------------------------------------------------
// FILE:		ping_request.go
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

	// PingRequest is ProxyRequest of MessageType
	// PingRequest.
	//
	// A PingRequest contains a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	PingRequest struct {
		*ProxyRequest
	}
)

// NewPingRequest is the default constructor for
// PingRequest
//
// returns *PingRequest -> pointer to a newly initialized
// PingReqeuest in memory
func NewPingRequest() *PingRequest {
	request := new(PingRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.PingRequest)
	request.SetReplyType(internal.PingReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (request *PingRequest) Clone() IProxyMessage {
	pingRequest := NewPingRequest()
	var messageClone IProxyMessage = pingRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (request *PingRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
}

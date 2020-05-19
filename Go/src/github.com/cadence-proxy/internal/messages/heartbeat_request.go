//-----------------------------------------------------------------------------
// FILE:		heartbeat_request.go
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

	// HeartbeatRequest is ProxyRequest of MessageType
	// HeartbeatRequest.
	//
	// A HeartbeatRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	HeartbeatRequest struct {
		*ProxyRequest
	}
)

// NewHeartbeatRequest is the default constructor for
// HeartbeatRequest
//
// returns *HeartbeatRequest -> pointer to a newly initialized
// HeartbeatReqeuest in memory
func NewHeartbeatRequest() *HeartbeatRequest {
	request := new(HeartbeatRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.HeartbeatRequest)
	request.SetReplyType(internal.HeartbeatReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *HeartbeatRequest) Clone() IProxyMessage {
	heartbeatRequest := NewHeartbeatRequest()
	var messageClone IProxyMessage = heartbeatRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *HeartbeatRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
}

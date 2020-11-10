//-----------------------------------------------------------------------------
// FILE:		start_worker_request.go
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

	// StartWorkerRequest is ProxyRequest of MessageType
	// StartWorkerRequest.
	//
	// A StartWorkerRequest contains a reference to a
	// ProxyReply struct in memory
	StartWorkerRequest struct {
		*ProxyRequest
	}
)

// NewStartWorkerRequest is the default constructor for a StartWorkerRequest
//
// returns *StartWorkerRequest -> a reference to a newly initialized
// StartWorkerRequest in memory
func NewStartWorkerRequest() *StartWorkerRequest {
	request := new(StartWorkerRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.StartWorkerRequest)
	request.SetReplyType(internal.StartWorkerReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *StartWorkerRequest) Clone() IProxyMessage {
	startWorkerRequest := NewStartWorkerRequest()
	var messageClone IProxyMessage = startWorkerRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *StartWorkerRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
}

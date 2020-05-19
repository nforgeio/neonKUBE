//-----------------------------------------------------------------------------
// FILE:		workflow_disconnect_context_request.go
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

	// WorkflowDisconnectContextRequest is WorkflowRequest of MessageType
	// WorkflowDisconnectContextRequest.
	//
	// A WorkflowDisconnectContextRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Commands cadence-proxy to replace the current workflow
	// context with context that is disconnected from the parent context.
	WorkflowDisconnectContextRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowDisconnectContextRequest is the default constructor for a WorkflowDisconnectContextRequest
//
// returns *WorkflowDisconnectContextRequest -> a reference to a newly initialized
// WorkflowDisconnectContextRequest in memory
func NewWorkflowDisconnectContextRequest() *WorkflowDisconnectContextRequest {
	request := new(WorkflowDisconnectContextRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowDisconnectContextRequest)
	request.SetReplyType(internal.WorkflowDisconnectContextReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowDisconnectContextRequest) Clone() IProxyMessage {
	workflowDisconnectContextRequest := NewWorkflowDisconnectContextRequest()
	var messageClone IProxyMessage = workflowDisconnectContextRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowDisconnectContextRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
}

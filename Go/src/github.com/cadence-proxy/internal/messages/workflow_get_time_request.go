//-----------------------------------------------------------------------------
// FILE:		workflow_get_time_request.go
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

	// WorkflowGetTimeRequest is WorkflowRequest of MessageType
	// WorkflowGetTimeRequest.
	//
	// A WorkflowGetTimeRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Requests the current workflow time.
	WorkflowGetTimeRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowGetTimeRequest is the default constructor for a WorkflowGetTimeRequest
//
// returns *WorkflowGetTimeRequest -> a reference to a newly initialized
// WorkflowGetTimeRequest in memory
func NewWorkflowGetTimeRequest() *WorkflowGetTimeRequest {
	request := new(WorkflowGetTimeRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowGetTimeRequest)
	request.SetReplyType(internal.WorkflowGetTimeReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowGetTimeRequest) Clone() IProxyMessage {
	workflowGetTimeRequest := NewWorkflowGetTimeRequest()
	var messageClone IProxyMessage = workflowGetTimeRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowGetTimeRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
}

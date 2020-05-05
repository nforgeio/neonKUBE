//-----------------------------------------------------------------------------
// FILE:		workflow_has_last_result_request.go
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

	// WorkflowHasLastResultRequest is WorkflowRequest of MessageType
	// WorkflowHasLastResultRequest.
	//
	// A WorkflowHasLastResultRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowHasLastResultRequest determines whether the last execution of the workflow has
	// a completion result.  This can be used by CRON workflows to determine whether the
	// last run returned a result.
	WorkflowHasLastResultRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowHasLastResultRequest is the default constructor for a WorkflowHasLastResultRequest
//
// returns *WorkflowHasLastResultRequest -> a reference to a newly initialized
// WorkflowHasLastResultRequest in memory
func NewWorkflowHasLastResultRequest() *WorkflowHasLastResultRequest {
	request := new(WorkflowHasLastResultRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowHasLastResultRequest)
	request.SetReplyType(internal.WorkflowHasLastResultReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowHasLastResultRequest) Clone() IProxyMessage {
	workflowHasLastResultRequest := NewWorkflowHasLastResultRequest()
	var messageClone IProxyMessage = workflowHasLastResultRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowHasLastResultRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
}

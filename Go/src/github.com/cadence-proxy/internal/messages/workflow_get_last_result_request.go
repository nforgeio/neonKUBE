//-----------------------------------------------------------------------------
// FILE:		workflow_get_last_result_request.go
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

	// WorkflowGetLastResultRequest is WorkflowRequest of MessageType
	// WorkflowGetLastResultRequest.
	//
	// A WorkflowGetLastResultRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowGetLastResultRequest returns the result from the last execution of the workflow.
	///  This can be used by CRON workflows to retrieve state from the last workflow run.
	WorkflowGetLastResultRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowGetLastResultRequest is the default constructor for a WorkflowGetLastResultRequest
//
// returns *WorkflowGetLastResultRequest -> a reference to a newly initialized
// WorkflowGetLastResultRequest in memory
func NewWorkflowGetLastResultRequest() *WorkflowGetLastResultRequest {
	request := new(WorkflowGetLastResultRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowGetLastResultRequest)
	request.SetReplyType(internal.WorkflowGetLastResultReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowGetLastResultRequest) Clone() IProxyMessage {
	workflowGetLastResultRequest := NewWorkflowGetLastResultRequest()
	var messageClone IProxyMessage = workflowGetLastResultRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowGetLastResultRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
}

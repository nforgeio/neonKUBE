//-----------------------------------------------------------------------------
// FILE:		workflow_future_ready_request.go
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

	// WorkflowFutureReadyRequest is WorkflowRequest of MessageType
	// WorkflowFutureReadyRequest.
	//
	// A WorkflowFutureReadyRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowFutureReadyRequest will pass all of the given data
	// necessary to get the execution result of a temporal workflow instance
	WorkflowFutureReadyRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowFutureReadyRequest is the default constructor for a WorkflowFutureReadyRequest
//
// returns *WorkflowFutureReadyRequest -> a reference to a newly initialized
// WorkflowFutureReadyRequest in memory
func NewWorkflowFutureReadyRequest() *WorkflowFutureReadyRequest {
	request := new(WorkflowFutureReadyRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowFutureReadyRequest)
	request.SetReplyType(internal.WorkflowFutureReadyReply)

	return request
}

// GetFutureOperationID gets a WorkflowFutureReadyRequest's FutureOperationID value
// from its properties map.  The ID of the original operation what
// has been submitted to Temporal and who's future has been returned.
//
// returns int64 -> the value of a WorkflowFutureReadyRequest's FutureOperationID
func (request *WorkflowFutureReadyRequest) GetFutureOperationID() int64 {
	return request.GetLongProperty("FutureOperationId")
}

// SetFutureOperationID sets an WorkflowFutureReadyRequest's FutureOperationID value
// in its properties map. The ID of the original operation what
// has been submitted to Temporal and who's future has been returned.
//
// param value int64 -> the value WorkflowFutureReadyRequest's FutureOperationID
func (request *WorkflowFutureReadyRequest) SetFutureOperationID(value int64) {
	request.SetLongProperty("FutureOperationId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowFutureReadyRequest) Clone() IProxyMessage {
	workflowFutureReadyRequest := NewWorkflowFutureReadyRequest()
	var messageClone IProxyMessage = workflowFutureReadyRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowFutureReadyRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowFutureReadyRequest); ok {
		v.SetFutureOperationID(request.GetFutureOperationID())
	}
}

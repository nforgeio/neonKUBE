//-----------------------------------------------------------------------------
// FILE:		workflow_queue_length_request.go
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

	// WorkflowQueueCloseRequest is WorkflowRequest of MessageType
	// WorkflowQueueCloseRequest.
	//
	// A WorkflowQueueCloseRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Closes a workflow queue.
	WorkflowQueueCloseRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowQueueCloseRequest is the default constructor for a WorkflowQueueCloseRequest
//
// returns *WorkflowQueueCloseRequest -> a reference to a newly initialized
// WorkflowQueueCloseRequest in memory
func NewWorkflowQueueCloseRequest() *WorkflowQueueCloseRequest {
	request := new(WorkflowQueueCloseRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowQueueCloseRequest)
	request.SetReplyType(internal.WorkflowQueueCloseReply)

	return request
}

// GetQueueID gets a WorkflowQueueCloseRequest's QueueID value
// from its properties map. Identifies the queue.
//
// returns int64 -> int64 QueueID.
func (request *WorkflowQueueCloseRequest) GetQueueID() int64 {
	return request.GetLongProperty("QueueId")
}

// SetQueueID sets a WorkflowQueueCloseRequest's QueueID value
// in its properties map. Identifies the queue.
//
// param value int64 -> int64 QueueID.
func (request *WorkflowQueueCloseRequest) SetQueueID(value int64) {
	request.SetLongProperty("QueueId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowQueueCloseRequest) Clone() IProxyMessage {
	workflowQueueCloseRequest := NewWorkflowQueueCloseRequest()
	var messageClone IProxyMessage = workflowQueueCloseRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowQueueCloseRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowQueueCloseRequest); ok {
		v.SetQueueID(request.GetQueueID())
	}
}

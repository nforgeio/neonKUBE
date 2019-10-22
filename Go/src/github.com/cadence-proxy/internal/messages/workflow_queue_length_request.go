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
	internal "github.com/cadence-proxy/internal"
)

type (

	// WorkflowQueueLengthRequest is WorkflowRequest of MessageType
	// WorkflowQueueLengthRequest.
	//
	// A WorkflowQueueLengthRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Gets the length of a workflow queue.
	WorkflowQueueLengthRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowQueueLengthRequest is the default constructor for a WorkflowQueueLengthRequest
//
// returns *WorkflowQueueLengthRequest -> a reference to a newly initialized
// WorkflowQueueLengthRequest in memory
func NewWorkflowQueueLengthRequest() *WorkflowQueueLengthRequest {
	request := new(WorkflowQueueLengthRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowQueueLengthRequest)
	request.SetReplyType(internal.WorkflowQueueLengthReply)

	return request
}

// GetQueueID gets a WorkflowQueueLengthRequest's QueueID value
// from its properties map. Identifies the queue.
//
// returns int64 -> int64 QueueID.
func (request *WorkflowQueueLengthRequest) GetQueueID() int64 {
	return request.GetLongProperty("QueueId")
}

// SetQueueID sets a WorkflowQueueLengthRequest's QueueID value
// in its properties map. Identifies the queue.
//
// param value int64 -> int64 QueueID.
func (request *WorkflowQueueLengthRequest) SetQueueID(value int64) {
	request.SetLongProperty("QueueId", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowQueueLengthRequest) Clone() IProxyMessage {
	workflowQueueLengthRequest := NewWorkflowQueueLengthRequest()
	var messageClone IProxyMessage = workflowQueueLengthRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowQueueLengthRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowQueueLengthRequest); ok {
		v.SetQueueID(request.GetQueueID())
	}
}

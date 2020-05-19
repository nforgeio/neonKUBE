//-----------------------------------------------------------------------------
// FILE:		workflow_queue_new_request.go
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

	// WorkflowQueueNewRequest is WorkflowRequest of MessageType
	// WorkflowQueueNewRequest.
	//
	// A WorkflowQueueNewRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Creates a new workflow queue
	WorkflowQueueNewRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowQueueNewRequest is the default constructor for a WorkflowQueueNewRequest
//
// returns *WorkflowQueueNewRequest -> a reference to a newly initialized
// WorkflowQueueNewRequest in memory
func NewWorkflowQueueNewRequest() *WorkflowQueueNewRequest {
	request := new(WorkflowQueueNewRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowQueueNewRequest)
	request.SetReplyType(internal.WorkflowQueueNewReply)

	return request
}

// GetQueueID gets a WorkflowQueueNewRequest's QueueID value
// from its properties map. Identifies the queue.
//
// returns int64 -> int64 QueueID.
func (request *WorkflowQueueNewRequest) GetQueueID() int64 {
	return request.GetLongProperty("QueueId")
}

// SetQueueID sets a WorkflowQueueNewRequest's QueueID value
// in its properties map. Identifies the queue.
//
// param value int64 -> int64 QueueID.
func (request *WorkflowQueueNewRequest) SetQueueID(value int64) {
	request.SetLongProperty("QueueId", value)
}

// GetCapacity gets a WorkflowQueueNewRequest's Capacity value
// from its properties map. Specifies the capacity of the queue.
//
// returns int32 -> int32 queue capacity.
func (request *WorkflowQueueNewRequest) GetCapacity() int32 {
	return request.GetIntProperty("Capacity")
}

// SetCapacity sets a WorkflowQueueNewRequest's Capacity value
// in its properties map. Specifies the capacity of the queue.
//
// param value int32 -> int32 queue capacity.
func (request *WorkflowQueueNewRequest) SetCapacity(value int32) {
	request.SetIntProperty("Capacity", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowQueueNewRequest) Clone() IProxyMessage {
	workflowQueueNewRequest := NewWorkflowQueueNewRequest()
	var messageClone IProxyMessage = workflowQueueNewRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowQueueNewRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowQueueNewRequest); ok {
		v.SetQueueID(request.GetQueueID())
		v.SetCapacity(request.GetCapacity())
	}
}

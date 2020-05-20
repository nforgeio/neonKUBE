//-----------------------------------------------------------------------------
// FILE:		workflow_queue_write_request.go
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

	// WorkflowQueueWriteRequest is WorkflowRequest of MessageType
	// WorkflowQueueWriteRequest.
	//
	// A WorkflowQueueWriteRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Writes data to a workflow queue.
	WorkflowQueueWriteRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowQueueWriteRequest is the default constructor for a WorkflowQueueWriteRequest
//
// returns *WorkflowQueueWriteRequest -> a reference to a newly initialized
// WorkflowQueueWriteRequest in memory
func NewWorkflowQueueWriteRequest() *WorkflowQueueWriteRequest {
	request := new(WorkflowQueueWriteRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowQueueWriteRequest)
	request.SetReplyType(internal.WorkflowQueueWriteReply)

	return request
}

// GetQueueID gets a WorkflowQueueWriteRequest's QueueID value
// from its properties map. Identifies the queue.
//
// returns int64 -> int64 QueueID.
func (request *WorkflowQueueWriteRequest) GetQueueID() int64 {
	return request.GetLongProperty("QueueId")
}

// SetQueueID sets a WorkflowQueueWriteRequest's QueueID value
// in its properties map. Identifies the queue.
//
// param value int64 -> int64 QueueID.
func (request *WorkflowQueueWriteRequest) SetQueueID(value int64) {
	request.SetLongProperty("QueueId", value)
}

// GetData gets a WorkflowQueueWriteRequest's Data value
// from its properties map, the data to be written to the queue.
//
// returns []byte -> []byte queue Data.
func (request *WorkflowQueueWriteRequest) GetData() []byte {
	return request.GetBytesProperty("Data")
}

// SetData sets a WorkflowQueueWriteRequest's Data value
// in its properties map, the data to be written to the queue.
//
// param value []byte -> []byte queue Data.
func (request *WorkflowQueueWriteRequest) SetData(value []byte) {
	request.SetBytesProperty("Data", value)
}

// GetNoBlock gets a WorkflowQueueWriteRequest's NoBlock value
// from its properties map, indicates whether the write operation should not block when
// the queue is full.
//
// returns bool -> bool queue NoBlock.
func (request *WorkflowQueueWriteRequest) GetNoBlock() bool {
	return request.GetBoolProperty("NoBlock")
}

// SetNoBlock sets a WorkflowQueueWriteRequest's NoBlock value
// in its properties map, indicates whether the write operation should not block when
// the queue is full.
//
// param value bool -> bool queue NoBlock.
func (request *WorkflowQueueWriteRequest) SetNoBlock(value bool) {
	request.SetBoolProperty("NoBlock", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowQueueWriteRequest) Clone() IProxyMessage {
	workflowQueueWriteRequest := NewWorkflowQueueWriteRequest()
	var messageClone IProxyMessage = workflowQueueWriteRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowQueueWriteRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowQueueWriteRequest); ok {
		v.SetQueueID(request.GetQueueID())
		v.SetData(request.GetData())
		v.SetNoBlock(request.GetNoBlock())
	}
}

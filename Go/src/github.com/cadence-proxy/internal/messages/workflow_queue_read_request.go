//-----------------------------------------------------------------------------
// FILE:		workflow_queue_read_request.go
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
	"time"

	internal "github.com/cadence-proxy/internal"
)

type (

	// WorkflowQueueReadRequest is WorkflowRequest of MessageType
	// WorkflowQueueReadRequest.
	//
	// A WorkflowQueueReadRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Reads data from a workflow queue.
	WorkflowQueueReadRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowQueueReadRequest is the default constructor for a WorkflowQueueReadRequest
//
// returns *WorkflowQueueReadRequest -> a reference to a newly initialized
// WorkflowQueueReadRequest in memory
func NewWorkflowQueueReadRequest() *WorkflowQueueReadRequest {
	request := new(WorkflowQueueReadRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowQueueReadRequest)
	request.SetReplyType(internal.WorkflowQueueReadReply)

	return request
}

// GetQueueID gets a WorkflowQueueReadRequest's QueueID value
// from its properties map. Identifies the queue.
//
// returns int64 -> int64 QueueID.
func (request *WorkflowQueueReadRequest) GetQueueID() int64 {
	return request.GetLongProperty("QueueId")
}

// SetQueueID sets a WorkflowQueueReadRequest's QueueID value
// in its properties map. Identifies the queue.
//
// param value int64 -> int64 QueueID.
func (request *WorkflowQueueReadRequest) SetQueueID(value int64) {
	request.SetLongProperty("QueueId", value)
}

// GetTimeout gets a WorkflowQueueReadRequest's Timeout value
// from its properties map. The maximum time to wait for
// a data item or zero time.Duration to wait indefinitiely.
//
// returns time.Duration -> time.Duration Timeout.
func (request *WorkflowQueueReadRequest) GetTimeout() time.Duration {
	return request.GetTimeSpanProperty("Timeout")
}

// SetTimeout sets a WorkflowQueueReadRequest's Timeout value
// in its properties map. The maximum time to wait for
// a data item or zero time.Duration to wait indefinitiely.
//
// param value time.Duration -> time.Duration Timeout.
func (request *WorkflowQueueReadRequest) SetTimeout(value time.Duration) {
	request.SetTimeSpanProperty("Timeout", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowQueueReadRequest) Clone() IProxyMessage {
	workflowQueueReadRequest := NewWorkflowQueueReadRequest()
	var messageClone IProxyMessage = workflowQueueReadRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowQueueReadRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowQueueReadRequest); ok {
		v.SetQueueID(request.GetQueueID())
		v.SetTimeout(request.GetTimeout())
	}
}

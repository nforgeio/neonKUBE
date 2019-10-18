//-----------------------------------------------------------------------------
// FILE:		workflow_queue_length_reply.go
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

	// WorkflowQueueLengthReply is a WorkflowReply of MessageType
	// WorkflowQueueLengthReply.  It holds a reference to a WorkflowReply
	// and is the reply type to a WorkflowQueueLengthRequest
	WorkflowQueueLengthReply struct {
		*WorkflowReply
	}
)

// NewWorkflowQueueLengthReply is the default constructor for
// a WorkflowQueueLengthReply
//
// returns *WorkflowQueueLengthReply -> a pointer to a newly initialized
// WorkflowQueueLengthReply.
func NewWorkflowQueueLengthReply() *WorkflowQueueLengthReply {
	reply := new(WorkflowQueueLengthReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowQueueLengthReply)

	return reply
}

// GetLength gets a WorkflowQueueLengthReply's Length value
// from its properties map. The number of items waiting in the queue.
//
// returns int32 -> int32 queue length.
func (reply *WorkflowQueueLengthReply) GetLength() int32 {
	return reply.GetIntProperty("Length")
}

// SetLength sets a WorkflowQueueLengthReply's Length value
// in its properties map. The number of items waiting in the queue.
//
// param value int32 -> int32 queue length.
func (reply *WorkflowQueueLengthReply) SetLength(value int32) {
	reply.SetIntProperty("Length", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowQueueLengthReply) Clone() IProxyMessage {
	workflowQueueLengthReply := NewWorkflowQueueLengthReply()
	var messageClone IProxyMessage = workflowQueueLengthReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowQueueLengthReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowQueueLengthReply); ok {
		v.SetLength(reply.GetLength())
	}
}

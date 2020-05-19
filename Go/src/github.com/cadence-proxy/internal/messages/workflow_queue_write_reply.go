//-----------------------------------------------------------------------------
// FILE:		workflow_queue_write_reply.go
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

	// WorkflowQueueWriteReply is a WorkflowReply of MessageType
	// WorkflowQueueWriteReply.  It holds a reference to a WorkflowReply
	// and is the reply type to a WorkflowQueueWriteRequest
	WorkflowQueueWriteReply struct {
		*WorkflowReply
	}
)

// NewWorkflowQueueWriteReply is the default constructor for
// a WorkflowQueueWriteReply
//
// returns *WorkflowQueueWriteReply -> a pointer to a newly initialized
// WorkflowQueueWriteReply.
func NewWorkflowQueueWriteReply() *WorkflowQueueWriteReply {
	reply := new(WorkflowQueueWriteReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowQueueWriteReply)

	return reply
}

// GetIsFull gets a WorkflowQueueWriteReply's IsFull value
// from its properties map, indicates when the queue is full
// and the item could not be written.
//
// returns bool -> bool queue is full.
func (reply *WorkflowQueueWriteReply) GetIsFull() bool {
	return reply.GetBoolProperty("IsFull")
}

// SetIsFull sets a WorkflowQueueWriteReply's IsFull value
// in its properties map, indicates when the queue is full
// and the item could not be written.
//
// param value bool -> bool queue is full.
func (reply *WorkflowQueueWriteReply) SetIsFull(value bool) {
	reply.SetBoolProperty("IsFull", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowQueueWriteReply) Clone() IProxyMessage {
	workflowQueueWriteReply := NewWorkflowQueueWriteReply()
	var messageClone IProxyMessage = workflowQueueWriteReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowQueueWriteReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowQueueWriteReply); ok {
		v.SetIsFull(reply.GetIsFull())
	}
}

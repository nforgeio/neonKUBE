//-----------------------------------------------------------------------------
// FILE:		workflow_queue_read_reply.go
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

	// WorkflowQueueReadReply is a WorkflowReply of MessageType
	// WorkflowQueueReadReply.  It holds a reference to a WorkflowReply
	// and is the reply type to a WorkflowQueueReadRequest
	WorkflowQueueReadReply struct {
		*WorkflowReply
	}
)

// NewWorkflowQueueReadReply is the default constructor for
// a WorkflowQueueReadReply
//
// returns *WorkflowQueueReadReply -> a pointer to a newly initialized
// WorkflowQueueReadReply.
func NewWorkflowQueueReadReply() *WorkflowQueueReadReply {
	reply := new(WorkflowQueueReadReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowQueueReadReply)

	return reply
}

// GetIsClosed gets a WorkflowQueueReadReply's IsClosed value
// from its properties map. Set to true when the queue has been closed.
//
// returns bool -> bool queue is closed.
func (reply *WorkflowQueueReadReply) GetIsClosed() bool {
	return reply.GetBoolProperty("IsClosed")
}

// SetIsClosed sets a WorkflowQueueReadReply's IsClosed value
// in its properties map. Set to true when the queue has been closed.
//
// param value bool -> bool queue is closed.
func (reply *WorkflowQueueReadReply) SetIsClosed(value bool) {
	reply.SetBoolProperty("IsClosed", value)
}

// GetData gets a WorkflowQueueReadReply's Data value
// from its properties map. The data to be written to the queue.
//
// returns []byte -> []byte queue Data.
func (reply *WorkflowQueueReadReply) GetData() []byte {
	return reply.GetBytesProperty("Data")
}

// SetData sets a WorkflowQueueReadReply's Data value
// in its properties map. The data to be written to the queue.
//
// param value []byte -> []byte queue Data.
func (reply *WorkflowQueueReadReply) SetData(value []byte) {
	reply.SetBytesProperty("Data", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowQueueReadReply) Clone() IProxyMessage {
	workflowQueueReadReply := NewWorkflowQueueReadReply()
	var messageClone IProxyMessage = workflowQueueReadReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowQueueReadReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowQueueReadReply); ok {
		v.SetIsClosed(reply.GetIsClosed())
		v.SetData(reply.GetData())
	}
}

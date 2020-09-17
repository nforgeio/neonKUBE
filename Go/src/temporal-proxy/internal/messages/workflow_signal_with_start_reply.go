//-----------------------------------------------------------------------------
// FILE:		workflow_signal_with_start_reply.go
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

	"go.temporal.io/sdk/workflow"
)

type (

	// WorkflowSignalWithStartReply is a WorkflowReply of MessageType
	// WorkflowSignalWithStartReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowSignalWithStartRequest
	WorkflowSignalWithStartReply struct {
		*WorkflowReply
	}
)

// NewWorkflowSignalWithStartReply is the default constructor for
// a WorkflowSignalWithStartReply
//
// returns *WorkflowSignalWithStartReply -> a pointer to a newly initialized
// WorkflowSignalWithStartReply in memory
func NewWorkflowSignalWithStartReply() *WorkflowSignalWithStartReply {
	reply := new(WorkflowSignalWithStartReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowSignalWithStartReply)

	return reply
}

// GetExecution gets the temporal workflow.Execution or nil
// from a WorkflowSignalWithStartReply's properties map.
//
// returns *workflow.Execution -> pointer to a temporal workflow.Execution
// struct housing the result of a workflow execution
func (reply *WorkflowSignalWithStartReply) GetExecution() *workflow.Execution {
	exe := new(workflow.Execution)
	err := reply.GetJSONProperty("Execution", exe)
	if err != nil {
		return nil
	}

	return exe
}

// SetExecution sets the temporal workflow.Execution or nil
// in a WorkflowSignalWithStartReply's properties map.
//
// param value *workflow.Execution -> pointer to a temporal workflow execution
// struct housing the result of a started workflow, to be set in the
// WorkflowSignalWithStartReply's properties map
func (reply *WorkflowSignalWithStartReply) SetExecution(value *workflow.Execution) {
	reply.SetJSONProperty("Execution", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from WorkflowReply.Build()
func (reply *WorkflowSignalWithStartReply) Build(e error, result ...interface{}) {
	reply.WorkflowReply.Build(e)
	if len(result) > 0 {
		if v, ok := result[0].(*workflow.Execution); ok {
			reply.SetExecution(v)
		}
	}
}

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowSignalWithStartReply) Clone() IProxyMessage {
	workflowSignalWithStartReply := NewWorkflowSignalWithStartReply()
	var messageClone IProxyMessage = workflowSignalWithStartReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowSignalWithStartReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowSignalWithStartReply); ok {
		v.SetExecution(reply.GetExecution())
	}
}

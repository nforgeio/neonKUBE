//-----------------------------------------------------------------------------
// FILE:		workflow_execute_reply.go
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
	"go.temporal.io/sdk/workflow"

	internal "temporal-proxy/internal"
)

type (

	// WorkflowExecuteReply is a WorkflowReply of MessageType
	// WorkflowExecuteReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowExecuteRequest
	WorkflowExecuteReply struct {
		*WorkflowReply
	}
)

// NewWorkflowExecuteReply is the default constructor for
// a WorkflowExecuteReply
//
// returns *WorkflowExecuteReply -> a pointer to a newly initialized
// WorkflowExecuteReply in memory
func NewWorkflowExecuteReply() *WorkflowExecuteReply {
	reply := new(WorkflowExecuteReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowExecuteReply)

	return reply
}

// GetExecution gets the workflow execution or nil
// from a WorkflowExecuteReply's properties map.
//
// returns *workflow.Execution -> pointer to a temporal workflow execution
// struct housing the result of a workflow execution
func (reply *WorkflowExecuteReply) GetExecution() *workflow.Execution {
	exe := new(workflow.Execution)
	err := reply.GetJSONProperty("Execution", exe)
	if err != nil {
		return nil
	}

	return exe
}

// SetExecution sets the workflow execution or nil
// in a WorkflowExecuteReply's properties map.
//
// param value *workflow.Execution -> pointer to a temporal workflow execution
// struct housing the result of a workflow execution, to be set in the
// WorkflowExecuteReply's properties map
func (reply *WorkflowExecuteReply) SetExecution(value *workflow.Execution) {
	reply.SetJSONProperty("Execution", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from WorkflowReply.Build()
func (reply *WorkflowExecuteReply) Build(e error, result ...interface{}) {
	reply.WorkflowReply.Build(e)
	if len(result) > 0 {
		if v, ok := result[0].(*workflow.Execution); ok {
			reply.SetExecution(v)
		}
	}
}

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowExecuteReply) Clone() IProxyMessage {
	workflowExecuteReply := NewWorkflowExecuteReply()
	var messageClone IProxyMessage = workflowExecuteReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowExecuteReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowExecuteReply); ok {
		v.SetExecution(reply.GetExecution())
	}
}

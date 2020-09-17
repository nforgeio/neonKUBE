//-----------------------------------------------------------------------------
// FILE:		workflow_execute_child_reply.go
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

	// WorkflowExecuteChildReply is a WorkflowReply of MessageType
	// WorkflowExecuteChildReply.  It holds a reference to a WorkflowReply in memory
	// and is the reply type to a WorkflowInvokeRequest
	WorkflowExecuteChildReply struct {
		*WorkflowReply
	}
)

// NewWorkflowExecuteChildReply is the default constructor for
// a WorkflowExecuteChildReply
//
// returns *WorkflowExecuteChildReply -> a pointer to a newly initialized
// WorkflowExecuteChildReply in memory
func NewWorkflowExecuteChildReply() *WorkflowExecuteChildReply {
	reply := new(WorkflowExecuteChildReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowExecuteChildReply)

	return reply
}

// GetChildID gets a WorkflowExecuteChildReply's ChildID value
// from its properties map. Identifies the child workflow.
//
// returns int64 -> long holding the value
// of a WorkflowExecuteChildReply's ChildID
func (reply *WorkflowExecuteChildReply) GetChildID() int64 {
	return reply.GetLongProperty("ChildId")
}

// SetChildID sets an WorkflowExecuteChildReply's ChildID value
// in its properties map. Identifies the child workflow.
//
// param value int64 -> long holding the value
// of a WorkflowExecuteChildReply's ChildID to be set in the
// WorkflowExecuteChildReply's properties map.
func (reply *WorkflowExecuteChildReply) SetChildID(value int64) {
	reply.SetLongProperty("ChildId", value)
}

// GetExecution gets the workflow execution or nil
// from a WorkflowExecuteChildReply's properties map.
//
// returns *workflow.Execution -> pointer to a temporal workflow execution
// struct housing the result of a workflow execution
func (reply *WorkflowExecuteChildReply) GetExecution() *workflow.Execution {
	exe := new(workflow.Execution)
	err := reply.GetJSONProperty("Execution", exe)
	if err != nil {
		return nil
	}

	return exe
}

// SetExecution sets the workflow execution or nil
// in a WorkflowExecuteChildReply's properties map.
//
// param value *workflow.Execution -> pointer to a temporal workflow execution
// struct housing the result of a workflow execution, to be set in the
// WorkflowExecuteChildReply's properties map
func (reply *WorkflowExecuteChildReply) SetExecution(value *workflow.Execution) {
	reply.SetJSONProperty("Execution", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from WorkflowReply.Build()
func (reply *WorkflowExecuteChildReply) Build(e error, result ...interface{}) {
	reply.WorkflowReply.Build(e)
	if len(result) > 0 {
		if v, ok := result[0].([]interface{}); ok {
			if _v, _ok := v[0].(int64); _ok {
				reply.SetChildID(_v)
			}
			if _v, _ok := v[1].(*workflow.Execution); _ok {
				reply.SetExecution(_v)
			}
		}
	}
}

// Clone inherits docs from ProxyMessage.Clone()
func (reply *WorkflowExecuteChildReply) Clone() IProxyMessage {
	workflowExecuteChildReply := NewWorkflowExecuteChildReply()
	var messageClone IProxyMessage = workflowExecuteChildReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (reply *WorkflowExecuteChildReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowExecuteChildReply); ok {
		v.SetChildID(reply.GetChildID())
		v.SetExecution(reply.GetExecution())
	}
}

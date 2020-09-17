//-----------------------------------------------------------------------------
// FILE:		workflow_describe_execution_reply.go
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
	go.temporal.io/api/workflowservice"

	internal "temporal-proxy/internal"
)

type (

	// WorkflowDescribeExecutionReply is a WorkflowReply of MessageType
	// WorkflowDescribeExecutionReply.  It holds a reference to a WorkflowReply in memory
	// and answers a WorkflowDescribeExecutionRequest.
	WorkflowDescribeExecutionReply struct {
		*WorkflowReply
	}
)

// NewWorkflowDescribeExecutionReply is the constructor for
// a WorkflowDescribeExecutionReply.
//
// returns *WorkflowDescribeExecutionReply -> a pointer to a newly initialized
// WorkflowDescribeExecutionReply.
func NewWorkflowDescribeExecutionReply() *WorkflowDescribeExecutionReply {
	reply := new(WorkflowDescribeExecutionReply)
	reply.WorkflowReply = NewWorkflowReply()
	reply.SetType(internal.WorkflowDescribeExecutionReply)

	return reply
}

// GetDetails gets the WorkflowDescribeExecutionReply's Details property from its
// properties map, the workflow execution details.
//
// returns *workflow.DescribeWorkflowExecutionResponse -> the *workflowservice.DescribeWorkflowExecutionResponse.
func (reply *WorkflowDescribeExecutionReply) GetDetails() *workflowservice.DescribeWorkflowExecutionResponse {
	resp := new(workflowservice.DescribeWorkflowExecutionResponse)
	err := reply.GetJSONProperty("Details", resp)
	if err != nil {
		return nil
	}

	return resp
}

// SetDetails sets the WorkflowDescribeExecutionReply's Details property in its
// properties map, the workflow execution details.
//
// param value *workflowservice.DescribeWorkflowExecutionResponse -> the *workflowservice.DescribeWorkflowExecutionResponse.
func (reply *WorkflowDescribeExecutionReply) SetDetails(value *workflowservice.DescribeWorkflowExecutionResponse) {
	reply.SetJSONProperty("Details", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from WorkflowReply.Build()
func (reply *WorkflowDescribeExecutionReply) Build(e error, result ...interface{}) {
	reply.WorkflowReply.Build(e)
	if len(result) > 0 {
		if v, ok := result[0].(*workflowservice.DescribeWorkflowExecutionResponse); ok {
			reply.SetDetails(v)
		}
	}
}

// Clone inherits docs from WorkflowReply.Clone()
func (reply *WorkflowDescribeExecutionReply) Clone() IProxyMessage {
	workflowDescribeExecutionReply := NewWorkflowDescribeExecutionReply()
	var messageClone IProxyMessage = workflowDescribeExecutionReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowReply.CopyTo()
func (reply *WorkflowDescribeExecutionReply) CopyTo(target IProxyMessage) {
	reply.WorkflowReply.CopyTo(target)
	if v, ok := target.(*WorkflowDescribeExecutionReply); ok {
		v.SetDetails(reply.GetDetails())
	}
}

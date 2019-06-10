//-----------------------------------------------------------------------------
// FILE:		workflow_execute_child_request.go
// CONTRIBUTOR: John C Burnes
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
	"go.uber.org/cadence/workflow"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// WorkflowExecuteChildRequest is WorkflowRequest of MessageType
	// WorkflowExecuteChildRequest.
	//
	// A WorkflowExecuteChildRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Executes a child workflow
	WorkflowExecuteChildRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowExecuteChildRequest is the default constructor for a WorkflowExecuteChildRequest
//
// returns *WorkflowExecuteChildRequest -> a reference to a newly initialized
// WorkflowExecuteChildRequest in memory
func NewWorkflowExecuteChildRequest() *WorkflowExecuteChildRequest {
	request := new(WorkflowExecuteChildRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(messagetypes.WorkflowExecuteChildRequest)
	request.SetReplyType(messagetypes.WorkflowExecuteChildReply)

	return request
}

// GetArgs gets a WorkflowExecuteChildRequest's Args field
// from its properties map.  Args is a []byte that
// specifies the child workflow arguments.
//
// returns []byte -> []byte representing workflow parameters or arguments
// for executing
func (request *WorkflowExecuteChildRequest) GetArgs() []byte {
	return request.GetBytesProperty("Args")
}

// SetArgs sets an WorkflowExecuteChildRequest's Args field
// from its properties map.  Args is a []byte that
// specifies the child workflow arguments.
//
// param value []byte -> []byte representing workflow parameters or arguments
// for executing
func (request *WorkflowExecuteChildRequest) SetArgs(value []byte) {
	request.SetBytesProperty("Args", value)
}

// GetOptions gets a WorkflowExecutionRequest's Options property
// from its properties map. Specifies the child workflow options.
//
// returns *workflow.ChildWorkflowOptions -> a pointer to a cadence
// workflow.ChidWorkflowOptions that specifies the child workflow options.
func (request *WorkflowExecuteChildRequest) GetOptions() *workflow.ChildWorkflowOptions {
	opts := new(workflow.ChildWorkflowOptions)
	err := request.GetJSONProperty("Options", opts)
	if err != nil {
		return nil
	}

	return opts
}

// SetOptions sets a WorkflowExecutionRequest's Options property
// in its properties map. Specifies the child workflow options.
//
// param value *workflow.ChildWorkflowOptions -> a pointer to a cadence
// workflow.ChidWorkflowOptions that specifies the child workflow options
// to be set in the WorkflowExecutionRequest's properties map
func (request *WorkflowExecuteChildRequest) SetOptions(value *workflow.ChildWorkflowOptions) {
	request.SetJSONProperty("Options", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowExecuteChildRequest) Clone() IProxyMessage {
	workflowExecuteChildRequest := NewWorkflowExecuteChildRequest()
	var messageClone IProxyMessage = workflowExecuteChildRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowExecuteChildRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowExecuteChildRequest); ok {
		v.SetArgs(request.GetArgs())
		v.SetOptions(request.GetOptions())
	}
}

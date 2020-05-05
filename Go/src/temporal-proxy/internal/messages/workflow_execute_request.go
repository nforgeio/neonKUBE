//-----------------------------------------------------------------------------
// FILE:		workflow_execute_request.go
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
	"go.temporal.io/temporal/client"

	internal "temporal-proxy/internal"
)

type (

	// WorkflowExecuteRequest is WorkflowRequest of MessageType
	// WorkflowExecuteRequest.
	//
	// A WorkflowExecuteRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowExecuteRequest will pass all of the given data and options
	// necessary to execute a temporal workflow via the temporal client
	WorkflowExecuteRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowExecuteRequest is the default constructor for a WorkflowExecuteRequest
//
// returns *WorkflowExecuteRequest -> a reference to a newly initialized
// WorkflowExecuteRequest in memory
func NewWorkflowExecuteRequest() *WorkflowExecuteRequest {
	request := new(WorkflowExecuteRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowExecuteRequest)
	request.SetReplyType(internal.WorkflowExecuteReply)

	return request
}

// GetDomain gets a WorkflowExecuteRequest's Domain value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowExecuteRequest's Domain
func (request *WorkflowExecuteRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets an WorkflowExecuteRequest's Domain value
// in its properties map
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowExecuteRequest's Domain
func (request *WorkflowExecuteRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
}

// GetWorkflow gets a WorkflowExecuteRequest's Workflow value
// from its properties map.  Identifies the workflow implementation to be started.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowExecuteRequest's Workflow
func (request *WorkflowExecuteRequest) GetWorkflow() *string {
	return request.GetStringProperty("Workflow")
}

// SetWorkflow sets a WorkflowExecuteRequest's Workflow value
// in its properties map. Identifies the workflow implementation to be started.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowExecuteRequest) SetWorkflow(value *string) {
	request.SetStringProperty("Workflow", value)
}

// GetArgs gets a WorkflowExecuteRequest's Args field
// from its properties map.  Args is a []byte that hold the arguments
// for executing a specific workflow
//
// returns []byte -> []byte representing workflow parameters or arguments
// for executing
func (request *WorkflowExecuteRequest) GetArgs() []byte {
	return request.GetBytesProperty("Args")
}

// SetArgs sets an WorkflowExecuteRequest's Args field
// from its properties map.  Args is a []byte that hold the arguments
// for executing a specific workflow
//
// param value []byte -> []byte representing workflow parameters or arguments
// for executing
func (request *WorkflowExecuteRequest) SetArgs(value []byte) {
	request.SetBytesProperty("Args", value)
}

// GetOptions gets a WorkflowExecutionRequest's start options
// used to execute a temporal workflow via the temporal workflow client
//
// returns client.StartWorkflowOptions -> a temporal client struct that contains the
// options for executing a workflow
func (request *WorkflowExecuteRequest) GetOptions() *client.StartWorkflowOptions {
	opts := new(client.StartWorkflowOptions)
	err := request.GetJSONProperty("Options", opts)
	if err != nil {
		return nil
	}

	return opts
}

// SetOptions sets a WorkflowExecutionRequest's start options
// used to execute a temporal workflow via the temporal workflow client
//
// param value client.StartWorkflowOptions -> a temporal client struct that contains the
// options for executing a workflow to be set in the WorkflowExecutionRequest's
// properties map
func (request *WorkflowExecuteRequest) SetOptions(value *client.StartWorkflowOptions) {
	request.SetJSONProperty("Options", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowExecuteRequest) Clone() IProxyMessage {
	workflowExecuteRequest := NewWorkflowExecuteRequest()
	var messageClone IProxyMessage = workflowExecuteRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowExecuteRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowExecuteRequest); ok {
		v.SetDomain(request.GetDomain())
		v.SetWorkflow(request.GetWorkflow())
		v.SetArgs(request.GetArgs())
		v.SetOptions(request.GetOptions())
	}
}

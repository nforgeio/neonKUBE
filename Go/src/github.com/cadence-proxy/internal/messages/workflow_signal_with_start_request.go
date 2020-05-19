//-----------------------------------------------------------------------------
// FILE:		workflow_signal_with_start_request.go
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

	"go.uber.org/cadence/client"
)

type (

	// WorkflowSignalWithStartRequest is WorkflowRequest of MessageType
	// WorkflowSignalWithStartRequest.
	//
	// A WorkflowSignalWithStartRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowSignalWithStartRequest will pass all of the given data and options
	// necessary to signal a cadence workflow with start via the cadence client
	WorkflowSignalWithStartRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowSignalWithStartRequest is the default constructor for a WorkflowSignalWithStartRequest
//
// returns *WorkflowSignalWithStartRequest -> a reference to a newly initialized
// WorkflowSignalWithStartRequest in memory
func NewWorkflowSignalWithStartRequest() *WorkflowSignalWithStartRequest {
	request := new(WorkflowSignalWithStartRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowSignalWithStartRequest)
	request.SetReplyType(internal.WorkflowSignalWithStartReply)

	return request
}

// GetWorkflowID gets a WorkflowSignalWithStartRequest's WorkflowID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalWithStartRequest's WorkflowID
func (request *WorkflowSignalWithStartRequest) GetWorkflowID() *string {
	return request.GetStringProperty("WorkflowId")
}

// SetWorkflowID sets an WorkflowSignalWithStartRequest's WorkflowID value
// in its properties map
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowSignalWithStartRequest's WorkflowID
func (request *WorkflowSignalWithStartRequest) SetWorkflowID(value *string) {
	request.SetStringProperty("WorkflowId", value)
}

// GetSignalName gets a WorkflowSignalWithStartRequest's SignalName value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalWithStartRequest's SignalName
func (request *WorkflowSignalWithStartRequest) GetSignalName() *string {
	return request.GetStringProperty("SignalName")
}

// SetSignalName sets a WorkflowSignalWithStartRequest's SignalName value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowSignalWithStartRequest) SetSignalName(value *string) {
	request.SetStringProperty("SignalName", value)
}

// GetSignalArgs gets a WorkflowSignalWithStartRequest's SignalArgs field
// from its properties map.  SignalArgs is a []byte that hold the arguments
// for executing a specific workflow
//
// returns []byte -> []byte representing workflow parameters or arguments
// for executing
func (request *WorkflowSignalWithStartRequest) GetSignalArgs() []byte {
	return request.GetBytesProperty("SignalArgs")
}

// SetSignalArgs sets an WorkflowSignalWithStartRequest's SignalArgs field
// from its properties map.  SignalArgs is a []byte that hold the arguments
// for signaling a specific workflow with start
//
// param value []byte -> []byte representing workflow parameters or arguments
// for executing
func (request *WorkflowSignalWithStartRequest) SetSignalArgs(value []byte) {
	request.SetBytesProperty("SignalArgs", value)
}

// GetOptions gets a WorkflowSignalWithStartRequest's start options
// used to signal a cadence workflow with start options via the client
//
// returns client.StartWorkflowOptions -> a cadence client struct that contains the
// options for executing a workflow
func (request *WorkflowSignalWithStartRequest) GetOptions() *client.StartWorkflowOptions {
	opts := new(client.StartWorkflowOptions)
	err := request.GetJSONProperty("Options", opts)
	if err != nil {
		return nil
	}

	return opts
}

// SetOptions sets a WorkflowSignalWithStartRequest's start options
// used to signal a cadence workflow with start options via the client
//
// param value client.StartWorkflowOptions -> a cadence client struct that contains the
// options for executing a workflow to be set in the WorkflowSignalWithStartRequest's
// properties map
func (request *WorkflowSignalWithStartRequest) SetOptions(value *client.StartWorkflowOptions) {
	request.SetJSONProperty("Options", value)
}

// GetWorkflowArgs gets a WorkflowSignalWithStartRequest's WorkflowArgs field
// from its properties map.  WorkflowArgs is a []byte that hold the arguments
// for executing a specific workflow
//
// returns []byte -> []byte representing workflow parameters or arguments
// for executing
func (request *WorkflowSignalWithStartRequest) GetWorkflowArgs() []byte {
	return request.GetBytesProperty("WorkflowArgs")
}

// SetWorkflowArgs sets an WorkflowSignalWithStartRequest's WorkflowArgs field
// from its properties map.  WorkflowArgs is a []byte that hold the arguments
// for signaling a specific workflow with start
//
// param value []byte -> []byte representing workflow parameters or arguments
// for executing
func (request *WorkflowSignalWithStartRequest) SetWorkflowArgs(value []byte) {
	request.SetBytesProperty("WorkflowArgs", value)
}

// GetWorkflow gets a WorkflowSignalWithStartRequest's Workflow value
// from its properties map. Identifies the workflow implementation to be started.
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalWithStartRequest's Workflow
func (request *WorkflowSignalWithStartRequest) GetWorkflow() *string {
	return request.GetStringProperty("Workflow")
}

// SetWorkflow sets a WorkflowSignalWithStartRequest's Workflow value
// in its properties map. Identifies the workflow implementation to be started.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowSignalWithStartRequest) SetWorkflow(value *string) {
	request.SetStringProperty("Workflow", value)
}

// GetDomain gets a WorkflowSignalWithStartRequest's Domain value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalWithStartRequest's Domain
func (request *WorkflowSignalWithStartRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets a WorkflowSignalWithStartRequest's Domain value
// in its properties map.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowSignalWithStartRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowSignalWithStartRequest) Clone() IProxyMessage {
	workflowSignalWithStartRequest := NewWorkflowSignalWithStartRequest()
	var messageClone IProxyMessage = workflowSignalWithStartRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowSignalWithStartRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSignalWithStartRequest); ok {
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetSignalName(request.GetSignalName())
		v.SetSignalArgs(request.GetSignalArgs())
		v.SetOptions(request.GetOptions())
		v.SetWorkflowArgs(request.GetWorkflowArgs())
		v.SetWorkflow(request.GetWorkflow())
		v.SetDomain(request.GetDomain())
	}
}

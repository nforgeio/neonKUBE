//-----------------------------------------------------------------------------
// FILE:		workflow_signal_request.go
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
)

type (

	// WorkflowSignalRequest is WorkflowRequest of MessageType
	// WorkflowSignalRequest.
	//
	// A WorkflowSignalRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowSignalRequest will pass all of the given data and options
	// necessary to signal a temporal workflow via the temporal client
	WorkflowSignalRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowSignalRequest is the default constructor for a WorkflowSignalRequest
//
// returns *WorkflowSignalRequest -> a reference to a newly initialized
// WorkflowSignalRequest in memory
func NewWorkflowSignalRequest() *WorkflowSignalRequest {
	request := new(WorkflowSignalRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowSignalRequest)
	request.SetReplyType(internal.WorkflowSignalReply)

	return request
}

// GetWorkflowID gets a WorkflowSignalRequest's WorkflowID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalRequest's WorkflowID
func (request *WorkflowSignalRequest) GetWorkflowID() *string {
	return request.GetStringProperty("WorkflowId")
}

// SetWorkflowID sets an WorkflowSignalRequest's WorkflowID value
// in its properties map
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowSignalRequest's WorkflowID
func (request *WorkflowSignalRequest) SetWorkflowID(value *string) {
	request.SetStringProperty("WorkflowId", value)
}

// GetRunID gets a WorkflowSignalRequest's RunID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalRequest's RunID
func (request *WorkflowSignalRequest) GetRunID() *string {
	return request.GetStringProperty("RunId")
}

// SetRunID sets a WorkflowSignalRequest's RunID value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowSignalRequest) SetRunID(value *string) {
	request.SetStringProperty("RunId", value)
}

// GetSignalName gets a WorkflowSignalRequest's SignalName value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalRequest's SignalName
func (request *WorkflowSignalRequest) GetSignalName() *string {
	return request.GetStringProperty("SignalName")
}

// SetSignalName sets a WorkflowSignalRequest's SignalName value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowSignalRequest) SetSignalName(value *string) {
	request.SetStringProperty("SignalName", value)
}

// GetSignalArgs gets a WorkflowSignalRequest's SignalArgs field
// from its properties map.  SignalArgs is a []byte holding the arguments
// for signaling a specific workflow
//
// returns []byte -> a []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowSignalRequest) GetSignalArgs() []byte {
	return request.GetBytesProperty("SignalArgs")
}

// SetSignalArgs sets an WorkflowSignalRequest's SignalArgs field
// from its properties map.  SignalArgs is a []byte holding the arguments
// for signaling a specific workflow
//
// param value []byte -> []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowSignalRequest) SetSignalArgs(value []byte) {
	request.SetBytesProperty("SignalArgs", value)
}

// GetNamespace gets a WorkflowSignalRequest's Namespace value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowSignalRequest's Namespace
func (request *WorkflowSignalRequest) GetNamespace() *string {
	return request.GetStringProperty("Namespace")
}

// SetNamespace sets a WorkflowSignalRequest's Namespace value
// in its properties map.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowSignalRequest) SetNamespace(value *string) {
	request.SetStringProperty("Namespace", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowSignalRequest) Clone() IProxyMessage {
	workflowSignalRequest := NewWorkflowSignalRequest()
	var messageClone IProxyMessage = workflowSignalRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowSignalRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowSignalRequest); ok {
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetRunID(request.GetRunID())
		v.SetSignalName(request.GetSignalName())
		v.SetSignalArgs(request.GetSignalArgs())
		v.SetNamespace(request.GetNamespace())
	}
}

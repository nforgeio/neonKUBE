//-----------------------------------------------------------------------------
// FILE:		workflow_terminate_request.go
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

	// WorkflowTerminateRequest is WorkflowRequest of MessageType
	// WorkflowTerminateRequest.
	//
	// A WorkflowTerminateRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowTerminateRequest will pass all of the given data and options
	// necessary to termainte a cadence workflow via the cadence client
	WorkflowTerminateRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowTerminateRequest is the default constructor for a WorkflowTerminateRequest
//
// returns *WorkflowTerminateRequest -> a reference to a newly initialized
// WorkflowTerminateRequest in memory
func NewWorkflowTerminateRequest() *WorkflowTerminateRequest {
	request := new(WorkflowTerminateRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowTerminateRequest)
	request.SetReplyType(internal.WorkflowTerminateReply)

	return request
}

// GetWorkflowID gets a WorkflowTerminateRequest's WorkflowID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowTerminateRequest's WorkflowID
func (request *WorkflowTerminateRequest) GetWorkflowID() *string {
	return request.GetStringProperty("WorkflowId")
}

// SetWorkflowID sets an WorkflowTerminateRequest's WorkflowID value
// in its properties map
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowTerminateRequest's WorkflowID
func (request *WorkflowTerminateRequest) SetWorkflowID(value *string) {
	request.SetStringProperty("WorkflowId", value)
}

// GetRunID gets a WorkflowTerminateRequest's RunID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowTerminateRequest's RunID
func (request *WorkflowTerminateRequest) GetRunID() *string {
	return request.GetStringProperty("RunId")
}

// SetRunID sets a WorkflowTerminateRequest's RunID value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowTerminateRequest) SetRunID(value *string) {
	request.SetStringProperty("RunId", value)
}

// GetReason gets a WorkflowTerminateRequest's Reason value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowTerminateRequest's Reason
func (request *WorkflowTerminateRequest) GetReason() *string {
	return request.GetStringProperty("Reason")
}

// SetReason sets a WorkflowTerminateRequest's Reason value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowTerminateRequest) SetReason(value *string) {
	request.SetStringProperty("Reason", value)
}

// GetDetails gets a WorkflowTerminateRequest's Details field
// from its properties map.  Details is a []byte holding the details for
// terminating a workflow
//
// returns []byte -> a []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowTerminateRequest) GetDetails() []byte {
	return request.GetBytesProperty("Details")
}

// SetDetails sets an WorkflowTerminateRequest's Details field
// from its properties map.  Details is a []byte holding the details for
// terminating a workflow
//
// param value []byte -> []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowTerminateRequest) SetDetails(value []byte) {
	request.SetBytesProperty("Details", value)
}

// GetDomain gets a WorkflowTerminateRequest's Domain value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowTerminateRequest's Domain
func (request *WorkflowTerminateRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets a WorkflowTerminateRequest's Domain value
// in its properties map.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowTerminateRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowTerminateRequest) Clone() IProxyMessage {
	workflowTerminateRequest := NewWorkflowTerminateRequest()
	var messageClone IProxyMessage = workflowTerminateRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowTerminateRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowTerminateRequest); ok {
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetRunID(request.GetRunID())
		v.SetReason(request.GetReason())
		v.SetDetails(request.GetDetails())
		v.SetDomain(request.GetDomain())
	}
}

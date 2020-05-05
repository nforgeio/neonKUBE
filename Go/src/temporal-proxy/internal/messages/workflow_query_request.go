//-----------------------------------------------------------------------------
// FILE:		workflow_query_request.go
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

	// WorkflowQueryRequest is WorkflowRequest of MessageType
	// WorkflowQueryRequest.
	//
	// A WorkflowQueryRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// A WorkflowQueryRequest will pass all of the given data and options
	// necessary to query a temporal workflow via the temporal client
	WorkflowQueryRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowQueryRequest is the default constructor for a WorkflowQueryRequest
//
// returns *WorkflowQueryRequest -> a reference to a newly initialized
// WorkflowQueryRequest in memory
func NewWorkflowQueryRequest() *WorkflowQueryRequest {
	request := new(WorkflowQueryRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowQueryRequest)
	request.SetReplyType(internal.WorkflowQueryReply)

	return request
}

// GetWorkflowID gets a WorkflowQueryRequest's WorkflowID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowQueryRequest's WorkflowID
func (request *WorkflowQueryRequest) GetWorkflowID() *string {
	return request.GetStringProperty("WorkflowId")
}

// SetWorkflowID sets an WorkflowQueryRequest's WorkflowID value
// in its properties map
//
// param value *string -> pointer to a string in memory holding the value
// of a WorkflowQueryRequest's WorkflowID
func (request *WorkflowQueryRequest) SetWorkflowID(value *string) {
	request.SetStringProperty("WorkflowId", value)
}

// GetRunID gets a WorkflowQueryRequest's RunID value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowQueryRequest's RunID
func (request *WorkflowQueryRequest) GetRunID() *string {
	return request.GetStringProperty("RunId")
}

// SetRunID sets a WorkflowQueryRequest's RunID value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowQueryRequest) SetRunID(value *string) {
	request.SetStringProperty("RunId", value)
}

// GetQueryName gets a WorkflowQueryRequest's QueryName value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowQueryRequest's QueryName
func (request *WorkflowQueryRequest) GetQueryName() *string {
	return request.GetStringProperty("QueryName")
}

// SetQueryName sets a WorkflowQueryRequest's QueryName value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowQueryRequest) SetQueryName(value *string) {
	request.SetStringProperty("QueryName", value)
}

// GetQueryArgs gets a WorkflowQueryRequest's QueryArgs field
// from its properties map.  QueryArgs is a []byte holding the arguments
// for querying a specific workflow
//
// returns []byte -> a []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowQueryRequest) GetQueryArgs() []byte {
	return request.GetBytesProperty("QueryArgs")
}

// SetQueryArgs sets an WorkflowQueryRequest's QueryArgs field
// from its properties map.  QueryArgs is a []byte holding the arguments
// for querying a specific workflow
//
// param value []byte -> []byte of representing workflow parameters
// or arguments for invoking
func (request *WorkflowQueryRequest) SetQueryArgs(value []byte) {
	request.SetBytesProperty("QueryArgs", value)
}

// GetDomain gets a WorkflowQueryRequest's Domain value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a WorkflowQueryRequest's Domain
func (request *WorkflowQueryRequest) GetDomain() *string {
	return request.GetStringProperty("Domain")
}

// SetDomain sets a WorkflowQueryRequest's Domain value
// in its properties map.
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *WorkflowQueryRequest) SetDomain(value *string) {
	request.SetStringProperty("Domain", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (request *WorkflowQueryRequest) Clone() IProxyMessage {
	workflowQueryRequest := NewWorkflowQueryRequest()
	var messageClone IProxyMessage = workflowQueryRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (request *WorkflowQueryRequest) CopyTo(target IProxyMessage) {
	request.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowQueryRequest); ok {
		v.SetWorkflowID(request.GetWorkflowID())
		v.SetRunID(request.GetRunID())
		v.SetQueryName(request.GetQueryName())
		v.SetQueryArgs(request.GetQueryArgs())
		v.SetDomain(request.GetDomain())
	}
}

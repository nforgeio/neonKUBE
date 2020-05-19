//-----------------------------------------------------------------------------
// FILE:		workflow_get_version_reply.go
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

	// WorkflowGetVersionRequest is WorkflowRequest of MessageType
	// WorkflowGetVersionRequest.
	//
	// A WorkflowGetVersionRequest contains a reference to a
	// WorkflowRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this WorkflowRequest
	//
	// Requests the current workflow time.
	WorkflowGetVersionRequest struct {
		*WorkflowRequest
	}
)

// NewWorkflowGetVersionRequest is the default constructor for a WorkflowGetVersionRequest
//
// returns *WorkflowGetVersionRequest -> a reference to a newly initialized
// WorkflowGetVersionRequest in memory
func NewWorkflowGetVersionRequest() *WorkflowGetVersionRequest {
	request := new(WorkflowGetVersionRequest)
	request.WorkflowRequest = NewWorkflowRequest()
	request.SetType(internal.WorkflowGetVersionRequest)
	request.SetReplyType(internal.WorkflowGetVersionReply)

	return request
}

// GetChangeID gets the ChangeID property from the WorkflowGetVersionRequest's
// properties map. Identifies change from one workflow
// implementation version to another.
//
// returns *string -> the value of the ChangeID property from
// the WorkflowGetVersionRequest's properties map.
func (reply *WorkflowGetVersionRequest) GetChangeID() *string {
	return reply.GetStringProperty("ChangeId")
}

// SetChangeID sets the ChangeID property in the WorkflowGetVersionRequest's
// properties map.  Identifies change from one workflow
// implementation version to another.
//
// param value *string -> the ChangeID to be set in the
// WorkflowGetVersionRequest's properties map.
func (reply *WorkflowGetVersionRequest) SetChangeID(value *string) {
	reply.SetStringProperty("ChangeId", value)
}

// GetMinSupported gets the MinSupported property from a WorkflowGetVersionRequest's
// properties map. Specifies the minimum supported workflow implementation version.
//
// returns int32 -> value of the MinSupported property
func (reply *WorkflowGetVersionRequest) GetMinSupported() int32 {
	return reply.GetIntProperty("MinSupported")
}

// SetMinSupported sets the MinSupported property in a WorkflowGetVersionRequest's
// properties map. Specifies the minimum supported workflow implementation version.
//
// param value int32 -> value of the MinSupported property to be set in the
// WorkflowGetVersionRequest's properties map
func (reply *WorkflowGetVersionRequest) SetMinSupported(value int32) {
	reply.SetIntProperty("MinSupported", value)
}

// GetMaxSupported gets the MaxSupported property from a WorkflowGetVersionRequest's
// properties map. Specifies the maximum supported workflow implementation version.
//
// returns int32 -> value of the MaxSupported property
func (reply *WorkflowGetVersionRequest) GetMaxSupported() int32 {
	return reply.GetIntProperty("MaxSupported")
}

// SetMaxSupported sets the MaxSupported property in a WorkflowGetVersionRequest's
// properties map. Specifies the maximum supported workflow implementation version.
//
// param value int32 -> value of the MaxSupported property to be set in the
// WorkflowGetVersionRequest's properties map
func (reply *WorkflowGetVersionRequest) SetMaxSupported(value int32) {
	reply.SetIntProperty("MaxSupported", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from WorkflowRequest.Clone()
func (reply *WorkflowGetVersionRequest) Clone() IProxyMessage {
	workflowGetVersionRequest := NewWorkflowGetVersionRequest()
	var messageClone IProxyMessage = workflowGetVersionRequest
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from WorkflowRequest.CopyTo()
func (reply *WorkflowGetVersionRequest) CopyTo(target IProxyMessage) {
	reply.WorkflowRequest.CopyTo(target)
	if v, ok := target.(*WorkflowGetVersionRequest); ok {
		v.SetChangeID(reply.GetChangeID())
		v.SetMinSupported(reply.GetMinSupported())
		v.SetMaxSupported(reply.GetMaxSupported())
	}
}

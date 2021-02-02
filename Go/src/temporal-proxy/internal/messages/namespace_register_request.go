//-----------------------------------------------------------------------------
// FILE:		namespace_register_request.go
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
	"time"
)

type (

	// NamespaceRegisterRequest is ProxyRequest of MessageType
	// NamespaceRegisterRequest.
	//
	// A NamespaceRegisterRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	NamespaceRegisterRequest struct {
		*ProxyRequest
	}
)

// NewNamespaceRegisterRequest is the default constructor for a NamespaceRegisterRequest
//
// returns *NamespaceRegisterRequest -> a reference to a newly initialized
// NamespaceRegisterRequest in memory
func NewNamespaceRegisterRequest() *NamespaceRegisterRequest {
	request := new(NamespaceRegisterRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.NamespaceRegisterRequest)
	request.SetReplyType(internal.NamespaceRegisterReply)

	return request
}

// GetName gets a NamespaceRegisterRequest's Name value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a NamespaceRegisterRequest's Name
func (request *NamespaceRegisterRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a NamespaceRegisterRequest's Name value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *NamespaceRegisterRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// GetDescription gets a NamespaceRegisterRequest's Description value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a NamespaceRegisterRequest's Description
func (request *NamespaceRegisterRequest) GetDescription() *string {
	return request.GetStringProperty("Description")
}

// SetDescription sets a NamespaceRegisterRequest's Description value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *NamespaceRegisterRequest) SetDescription(value *string) {
	request.SetStringProperty("Description", value)
}

// GetOwnerEmail gets a NamespaceRegisterRequest's OwnerEmail value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a NamespaceRegisterRequest's OwnerEmail
func (request *NamespaceRegisterRequest) GetOwnerEmail() *string {
	return request.GetStringProperty("OwnerEmail")
}

// SetOwnerEmail sets a NamespaceRegisterRequest's OwnerEmail value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *NamespaceRegisterRequest) SetOwnerEmail(value *string) {
	request.SetStringProperty("OwnerEmail", value)
}

// GetWorkflowExecutionRetentionPeriod gets a NamespaceRegisterRequest's WorkflowExecutionRetentionPeriod value
// from its properties map
//
// returns time.Duration -> time.Duration indicating the complete workflow history retention
// period
func (request *NamespaceRegisterRequest) GetWorkflowExecutionRetentionPeriod() time.Duration {
	return request.GetTimeSpanProperty("WorkflowExecutionRetentionPeriod")
}

// SetWorkflowExecutionRetentionPeriod sets a NamespaceRegisterRequest's EmitMetrics value
// in its properties map
//
// param value time.Duration -> time.Duration value to be set in the properties map
func (request *NamespaceRegisterRequest) SetWorkflowExecutionRetentionPeriod(value time.Duration) {
	request.SetTimeSpanProperty("WorkflowExecutionRetentionPeriod", value)
}

// GetSecurityToken gets a NamespaceRegisterRequest's SecurityToken value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a NamespaceRegisterRequest's SecurityToken
func (request *NamespaceRegisterRequest) GetSecurityToken() *string {
	return request.GetStringProperty("SecurityToken")
}

// SetSecurityToken sets a NamespaceRegisterRequest's SecurityToken value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *NamespaceRegisterRequest) SetSecurityToken(value *string) {
	request.SetStringProperty("SecurityToken", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *NamespaceRegisterRequest) Clone() IProxyMessage {
	namespaceRegisterRequest := NewNamespaceRegisterRequest()
	var messageClone IProxyMessage = namespaceRegisterRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *NamespaceRegisterRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*NamespaceRegisterRequest); ok {
		v.SetName(request.GetName())
		v.SetDescription(request.GetDescription())
		v.SetOwnerEmail(request.GetOwnerEmail())
		v.SetWorkflowExecutionRetentionPeriod(request.GetWorkflowExecutionRetentionPeriod())
		v.SetSecurityToken(request.GetSecurityToken())
	}
}

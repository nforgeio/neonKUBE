//-----------------------------------------------------------------------------
// FILE:		domain_register_request.go
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
	messagetypes "github.com/cadence-proxy/internal/messages/types"
)

type (

	// DomainRegisterRequest is ProxyRequest of MessageType
	// DomainRegisterRequest.
	//
	// A DomainRegisterRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	DomainRegisterRequest struct {
		*ProxyRequest
	}
)

// NewDomainRegisterRequest is the default constructor for a DomainRegisterRequest
//
// returns *DomainRegisterRequest -> a reference to a newly initialized
// DomainRegisterRequest in memory
func NewDomainRegisterRequest() *DomainRegisterRequest {
	request := new(DomainRegisterRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(messagetypes.DomainRegisterRequest)
	request.SetReplyType(messagetypes.DomainRegisterReply)

	return request
}

// GetName gets a DomainRegisterRequest's Name value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a DomainRegisterRequest's Name
func (request *DomainRegisterRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a DomainRegisterRequest's Name value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *DomainRegisterRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// GetDescription gets a DomainRegisterRequest's Description value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a DomainRegisterRequest's Description
func (request *DomainRegisterRequest) GetDescription() *string {
	return request.GetStringProperty("Description")
}

// SetDescription sets a DomainRegisterRequest's Description value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *DomainRegisterRequest) SetDescription(value *string) {
	request.SetStringProperty("Description", value)
}

// GetOwnerEmail gets a DomainRegisterRequest's OwnerEmail value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a DomainRegisterRequest's OwnerEmail
func (request *DomainRegisterRequest) GetOwnerEmail() *string {
	return request.GetStringProperty("OwnerEmail")
}

// SetOwnerEmail sets a DomainRegisterRequest's OwnerEmail value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *DomainRegisterRequest) SetOwnerEmail(value *string) {
	request.SetStringProperty("OwnerEmail", value)
}

// GetEmitMetrics gets a DomainRegisterRequest's EmitMetrics value
// from its properties map
//
// returns bool -> bool indicating whether or not to enable metrics
func (request *DomainRegisterRequest) GetEmitMetrics() bool {
	return request.GetBoolProperty("EmitMetrics")
}

// SetEmitMetrics sets a DomainRegisterRequest's EmitMetrics value
// in its properties map
//
// param value bool -> bool value to be set in the properties map
func (request *DomainRegisterRequest) SetEmitMetrics(value bool) {
	request.SetBoolProperty("EmitMetrics", value)
}

// GetRetentionDays gets a DomainRegisterRequest's RetentionDays value
// from its properties map
//
// returns int32 -> int32 indicating the complete workflow history retention
// period in days
func (request *DomainRegisterRequest) GetRetentionDays() int32 {
	return request.GetIntProperty("RetentionDays")
}

// SetRetentionDays sets a DomainRegisterRequest's EmitMetrics value
// in its properties map
//
// param value int32 -> int32 value to be set in the properties map
func (request *DomainRegisterRequest) SetRetentionDays(value int32) {
	request.SetIntProperty("RetentionDays", value)
}

// GetSecurityToken gets a DomainRegisterRequest's SecurityToken value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a DomainRegisterRequest's SecurityToken
func (request *DomainRegisterRequest) GetSecurityToken() *string {
	return request.GetStringProperty("SecurityToken")
}

// SetSecurityToken sets a DomainRegisterRequest's SecurityToken value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *DomainRegisterRequest) SetSecurityToken(value *string) {
	request.SetStringProperty("SecurityToken", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *DomainRegisterRequest) Clone() IProxyMessage {
	domainRegisterRequest := NewDomainRegisterRequest()
	var messageClone IProxyMessage = domainRegisterRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *DomainRegisterRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*DomainRegisterRequest); ok {
		v.SetName(request.GetName())
		v.SetDescription(request.GetDescription())
		v.SetOwnerEmail(request.GetOwnerEmail())
		v.SetEmitMetrics(request.GetEmitMetrics())
		v.SetRetentionDays(request.GetRetentionDays())
		v.SetSecurityToken(request.GetSecurityToken())
	}
}

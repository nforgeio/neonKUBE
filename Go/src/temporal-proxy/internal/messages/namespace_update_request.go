//-----------------------------------------------------------------------------
// FILE:		namespace_update_request.go
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

	"go.temporal.io/api/namespace/v1"
	"go.temporal.io/api/replication/v1"
)

type (

	// NamespaceUpdateRequest is ProxyRequest of MessageType
	// NamespaceUpdateRequest.
	//
	// A NamespaceUpdateRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	NamespaceUpdateRequest struct {
		*ProxyRequest
	}
)

// NewNamespaceUpdateRequest is the default constructor for a NamespaceUpdateRequest
//
// returns *NamespaceUpdateRequest -> a reference to a newly initialized
// NamespaceUpdateRequest in memory
func NewNamespaceUpdateRequest() *NamespaceUpdateRequest {
	request := new(NamespaceUpdateRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.NamespaceUpdateRequest)
	request.SetReplyType(internal.NamespaceUpdateReply)

	return request
}

// GetName gets a NamespaceUpdateRequest's Name value
// from its properties map
//
// returns *string Name
func (request *NamespaceUpdateRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a NamespaceUpdateRequest's Name value
// in its properties map
//
// param value *string
func (request *NamespaceUpdateRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// GetUpdateNamespaceInfo gets a NamespaceUpdateRequest's UpdateNamespaceInfo field
// from its properties map.
//
// returns *namespace.UpdateNamespaceInfo
func (request *NamespaceUpdateRequest) GetUpdateNamespaceInfo() *namespace.UpdateNamespaceInfo {
	info := new(namespace.UpdateNamespaceInfo)
	err := request.GetJSONProperty("UpdateNamespaceInfo", info)
	if err != nil {
		return nil
	}
	return info
}

// SetUpdateNamespaceInfo sets an NamespaceUpdateRequest's UpdateNamespaceInfo field
// from its properties map.
//
// param value namespace.UpdateNamespaceInfo
func (request *NamespaceUpdateRequest) SetUpdateNamespaceInfo(value *namespace.UpdateNamespaceInfo) {
	request.SetJSONProperty("UpdateNamespaceInfo", value)
}

// GetNamespaceReplicationConfig gets a NamespaceUpdateRequest's ReplicationConfig field
// from its properties map.
//
// returns *replication.NamespaceReplicationConfig.
func (request *NamespaceUpdateRequest) GetNamespaceReplicationConfig() *replication.NamespaceReplicationConfig {
	config := new(replication.NamespaceReplicationConfig)
	err := request.GetJSONProperty("NamespaceReplicationConfig", config)
	if err != nil {
		return nil
	}
	return config
}

// SetNamespaceReplicationConfig sets an NamespaceUpdateRequest's NamespaceReplicationConfig field
// from its properties map.
//
// param value *replication.NamespaceReplicationConfig
func (request *NamespaceUpdateRequest) SetNamespaceReplicationConfig(value *replication.NamespaceReplicationConfig) {
	request.SetJSONProperty("NamespaceReplicationConfig", value)
}

// GetNamespaceConfig gets a NamespaceUpdateRequest's NamespaceConfig field
// from its properties map.
//
// returns *namespace.NamespaceConfig
func (request *NamespaceUpdateRequest) GetNamespaceConfig() *namespace.NamespaceConfig {
	config := new(namespace.NamespaceConfig)
	err := request.GetJSONProperty("NamespaceConfig", config)
	if err != nil {
		return nil
	}
	return config
}

// SetNamespaceConfig sets an NamespaceUpdateRequest's NamespaceConfig field
// from its properties map.
//
// param value namespace.NamespaceConfig
func (request *NamespaceUpdateRequest) SetNamespaceConfig(value *namespace.NamespaceConfig) {
	request.SetJSONProperty("NamespaceConfig", value)
}

// GetSecurityToken gets a NamespaceUpdateRequest's SecurityToken value
// from its properties map
//
// returns *string SecurityToken
func (request *NamespaceUpdateRequest) GetSecurityToken() *string {
	return request.GetStringProperty("SecurityToken")
}

// SetSecurityToken sets a NamespaceUpdateRequest's SecurityToken value
// in its properties map
//
// param value *string
func (request *NamespaceUpdateRequest) SetSecurityToken(value *string) {
	request.SetStringProperty("SecurityToken", value)
}

// GetDeleteBadBinary gets a NamespaceUpdateRequest's DeleteBadBinary value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a NamespaceUpdateRequest's DeleteBadBinary
func (request *NamespaceUpdateRequest) GetDeleteBadBinary() *string {
	return request.GetStringProperty("DeleteBadBinary")
}

// SetDeleteBadBinary sets a NamespaceUpdateRequest's DeleteBadBinary value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *NamespaceUpdateRequest) SetDeleteBadBinary(value *string) {
	request.SetStringProperty("DeleteBadBinary", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *NamespaceUpdateRequest) Clone() IProxyMessage {
	namespaceUpdateRequest := NewNamespaceUpdateRequest()
	var messageClone IProxyMessage = namespaceUpdateRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *NamespaceUpdateRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*NamespaceUpdateRequest); ok {
		v.SetName(request.GetName())
		v.SetNamespaceReplicationConfig(request.GetNamespaceReplicationConfig())
		v.SetUpdateNamespaceInfo(request.GetUpdateNamespaceInfo())
		v.SetNamespaceConfig(request.GetNamespaceConfig())
		v.SetSecurityToken(request.GetSecurityToken())
		v.SetDeleteBadBinary(request.GetDeleteBadBinary())
	}
}

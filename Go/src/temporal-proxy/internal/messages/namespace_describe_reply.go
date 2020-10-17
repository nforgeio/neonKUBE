//-----------------------------------------------------------------------------
// FILE:		namespace_describe_reply.go
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
	"strings"
	internal "temporal-proxy/internal"

	"go.temporal.io/api/enums/v1"
	"go.temporal.io/api/namespace/v1"
	"go.temporal.io/api/workflowservice/v1"
)

type (

	// NamespaceDescribeReply is a ProxyReply of MessageType
	// NamespaceDescribeReply.  It holds a reference to a ProxyReply in memory
	NamespaceDescribeReply struct {
		*ProxyReply
	}
)

// NewNamespaceDescribeReply is the default constructor for
// a NamespaceDescribeReply
//
// returns *NamespaceDescribeReply -> a pointer to a newly initialized
// NamespaceDescribeReply in memory
func NewNamespaceDescribeReply() *NamespaceDescribeReply {
	reply := new(NamespaceDescribeReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(internal.NamespaceDescribeReply)

	return reply
}

// GetNamespaceInfoName gets the NamespaceInfoName property as a string
// pointer from a NamespaceDescribeReply's properties map
//
// returns *string -> pointer to a string from a NamespaceDescribeReply's properties map
func (reply *NamespaceDescribeReply) GetNamespaceInfoName() *string {
	return reply.GetStringProperty("NamespaceInfoName")
}

// SetNamespaceInfoName sets the NamespaceInfoName property as a string
// pointer in a NamespaceDescribeReply's properties map
//
// param value *string -> pointer to the string value to set
// as the NamespaceDescribeReply's NamespaceInfoName in its properties map
func (reply *NamespaceDescribeReply) SetNamespaceInfoName(value *string) {
	reply.SetStringProperty("NamespaceInfoName", value)
}

// GetNamespaceInfoDescription gets the NamespaceInfoDescription property as a string
// pointer from a NamespaceDescribeReply's properties map
//
// returns *string -> pointer to a string from a NamespaceDescribeReply's properties map
func (reply *NamespaceDescribeReply) GetNamespaceInfoDescription() *string {
	return reply.GetStringProperty("NamespaceInfoDescription")
}

// SetNamespaceInfoDescription sets the NamespaceInfoDescription property as a string
// pointer in a NamespaceDescribeReply's properties map
//
// param value *string -> pointer to the string value to set
// as the NamespaceDescribeReply's NamespaceInfoDescription in its properties map
func (reply *NamespaceDescribeReply) SetNamespaceInfoDescription(value *string) {
	reply.SetStringProperty("NamespaceInfoDescription", value)
}

// GetNamespaceInfoStatus gets the NamespaceInfoStatus property as a string
// pointer from a NamespaceDescribeReply's properties map.
//
// returns enums.NamespaceState -> status of the specified namespace.
func (reply *NamespaceDescribeReply) GetNamespaceInfoStatus() enums.NamespaceState {
	namespaceStatusPtr := reply.GetStringProperty("NamespaceInfoStatus")
	if namespaceStatusPtr == nil {
		return enums.NAMESPACE_STATE_REGISTERED
	}

	return StringToNamespaceStatus(*namespaceStatusPtr)
}

// SetNamespaceInfoStatus sets the NamespaceInfoStatus property as a string
// pointer in a NamespaceDescribeReply's properties map
//
// param value enums.NamespaceState -> status of the specified namespace.
func (reply *NamespaceDescribeReply) SetNamespaceInfoStatus(value enums.NamespaceState) {
	status := value.String()
	reply.SetStringProperty("NamespaceInfoStatus", &status)
}

// GetNamespaceInfoOwnerEmail gets the NamespaceInfoOwnerEmail property as a string
// pointer from a NamespaceDescribeReply's properties map
//
// returns *string -> pointer to a string from a NamespaceDescribeReply's properties map
func (reply *NamespaceDescribeReply) GetNamespaceInfoOwnerEmail() *string {
	return reply.GetStringProperty("NamespaceInfoOwnerEmail")
}

// SetNamespaceInfoOwnerEmail sets the NamespaceInfoOwnerEmail property as a string
// pointer in a NamespaceDescribeReply's properties map
//
// param value *string -> pointer to the string value to set
// as the NamespaceDescribeReply's NamespaceInfoOwnerEmail in its properties map
func (reply *NamespaceDescribeReply) SetNamespaceInfoOwnerEmail(value *string) {
	reply.SetStringProperty("NamespaceInfoOwnerEmail", value)
}

// GetNamespaceConfig gets a NamespaceDescribeReply's NamespaceConfig field
// from its properties map. NamespaceConfig is the namespace.NamespaceConfig to set in the activity
// complete call.
//
// returns *namespace.NamespaceConfig -> namespace.NamespaceConfig to set in activity complete
func (reply *NamespaceDescribeReply) GetNamespaceConfig() *namespace.NamespaceConfig {
	config := new(namespace.NamespaceConfig)
	err := reply.GetJSONProperty("NamespaceConfig", &config)
	if err != nil {
		return nil
	}
	return config
}

// SetNamespaceConfig sets an NamespaceDescribeReply's NamespaceConfig field
// from its properties map.  NamespaceConfig is the namespace.NamespaceConfig to set in the activity
// complete call.
//
// param value namespace.NamespaceConfig -> namespace.NamespaceConfig value to set in activity complete
func (reply *NamespaceDescribeReply) SetNamespaceConfig(value *namespace.NamespaceConfig) {
	reply.SetJSONProperty("NamespaceConfig", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from ProxyReply.Build()
func (reply *NamespaceDescribeReply) Build(e error, result ...interface{}) {
	reply.ProxyReply.Build(e)
	if len(result) > 0 {
		if v, ok := result[0].(*workflowservice.DescribeNamespaceResponse); ok {
			reply.SetNamespaceInfoName(&v.NamespaceInfo.Name)
			reply.SetNamespaceInfoDescription(&v.NamespaceInfo.Description)
			reply.SetNamespaceInfoStatus(v.NamespaceInfo.State)
			reply.SetNamespaceConfig(v.Config)
			reply.SetNamespaceInfoOwnerEmail(&v.NamespaceInfo.OwnerEmail)
		}
	}
}

// Clone inherits docs from ProxyReply.Clone()
func (reply *NamespaceDescribeReply) Clone() IProxyMessage {
	namespaceDescribeReply := NewNamespaceDescribeReply()
	var messageClone IProxyMessage = namespaceDescribeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *NamespaceDescribeReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(*NamespaceDescribeReply); ok {
		v.SetNamespaceInfoName(reply.GetNamespaceInfoName())
		v.SetNamespaceConfig(reply.GetNamespaceConfig())
		v.SetNamespaceInfoDescription(reply.GetNamespaceInfoDescription())
		v.SetNamespaceInfoStatus(reply.GetNamespaceInfoStatus())
		v.SetNamespaceInfoOwnerEmail(reply.GetNamespaceInfoOwnerEmail())
	}
}

// StringToNamespaceStatus takes a valid domain status
// as a string and converts it into a domain status
// if possible
func StringToNamespaceStatus(value string) enums.NamespaceState {
	value = strings.ToUpper(value)
	switch value {
	case "REGISTERED":
		return enums.NAMESPACE_STATE_REGISTERED
	case "DEPRECATED":
		return enums.NAMESPACE_STATE_DEPRECATED
	case "DELETED":
		return enums.NAMESPACE_STATE_DELETED
	default:
		return enums.NAMESPACE_STATE_UNSPECIFIED
	}
}

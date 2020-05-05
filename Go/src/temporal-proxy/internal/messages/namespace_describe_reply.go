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
	internal "temporal-proxy/internal"
	proxyclient "temporal-proxy/internal/temporal/client"

	namespace "go.temporal.io/temporal-proto/namespace"
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
// returns namespace.NamespaceStatus -> status of the specified namespace.
func (reply *NamespaceDescribeReply) GetNamespaceInfoStatus() namespace.NamespaceStatus {
	namespaceStatusPtr := reply.GetStringProperty("NamespaceInfoStatus")
	if namespaceStatusPtr == nil {
		return namespace.NamespaceStatus_Registered
	}

	return proxyclient.StringToNamespaceStatus(*namespaceStatusPtr)
}

// SetNamespaceInfoStatus sets the NamespaceInfoStatus property as a string
// pointer in a NamespaceDescribeReply's properties map
//
// param value namespace.NamespaceStatus -> status of the specified namespace.
func (reply *NamespaceDescribeReply) SetNamespaceInfoStatus(value namespace.NamespaceStatus) {
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

// GetConfigurationRetentionDays gets the ConfigurationRetentionDays property
// as an int32 from a NamespaceDescribeReply's properties map
//
// returns int32 -> int32 namespace retention in days value from a NamespaceDescribeReply's
// properties map
func (reply *NamespaceDescribeReply) GetConfigurationRetentionDays() int32 {
	return reply.GetIntProperty("ConfigurationRetentionDays")
}

// SetConfigurationRetentionDays sets the ConfigurationRetentionDays property as
// an int32 pointer in a NamespaceDescribeReply's properties map
//
// param value int32 -> int32 namespace retention in days to set
// as the NamespaceDescribeReply's ConfigurationRetentionDays in its properties map
func (reply *NamespaceDescribeReply) SetConfigurationRetentionDays(value int32) {
	reply.SetIntProperty("ConfigurationRetentionDays", value)
}

// GetConfigurationEmitMetrics gets the ConfigurationEmitMetrics property
// as a bool from a NamespaceDescribeReply's properties map
//
// returns bool -> bool ConfigurationEmitMetrics value from a NamespaceDescribeReply's
// properties map
func (reply *NamespaceDescribeReply) GetConfigurationEmitMetrics() bool {
	return reply.GetBoolProperty("ConfigurationEmitMetrics")
}

// SetConfigurationEmitMetrics sets the ConfigurationEmitMetrics property as
// a bool in a NamespaceDescribeReply's properties map
//
// param value bool -> bool that enables metric generation
// as the NamespaceDescribeReply's ConfigurationEmitMetrics in its properties map
func (reply *NamespaceDescribeReply) SetConfigurationEmitMetrics(value bool) {
	reply.SetBoolProperty("ConfigurationEmitMetrics", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

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
		v.SetConfigurationEmitMetrics(reply.GetConfigurationEmitMetrics())
		v.SetConfigurationRetentionDays(reply.GetConfigurationRetentionDays())
		v.SetNamespaceInfoDescription(reply.GetNamespaceInfoDescription())
		v.SetNamespaceInfoStatus(reply.GetNamespaceInfoStatus())
		v.SetNamespaceInfoOwnerEmail(reply.GetNamespaceInfoOwnerEmail())
	}
}

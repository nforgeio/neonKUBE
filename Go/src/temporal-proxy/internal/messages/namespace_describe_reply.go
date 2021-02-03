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

	"go.temporal.io/api/namespace/v1"
	"go.temporal.io/api/replication/v1"
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
// returns *NamespaceDescribeReply
func NewNamespaceDescribeReply() *NamespaceDescribeReply {
	reply := new(NamespaceDescribeReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(internal.NamespaceDescribeReply)

	return reply
}

// GetNamespaceInfo gets the NamespaceInfo property.
//
// returns *NamespaceInfo.
func (reply *NamespaceDescribeReply) GetNamespaceInfo() *namespace.NamespaceInfo {
	info := new(namespace.NamespaceInfo)
	err := reply.GetJSONProperty("NamespaceInfo", info)
	if err != nil {
		return nil
	}

	return info
}

// SetNamespaceInfo sets the NamespaceInfo property as a string
// pointer in a NamespaceDescribeReply's properties map
//
// param value *string
func (reply *NamespaceDescribeReply) SetNamespaceInfo(value *namespace.NamespaceInfo) {
	reply.SetJSONProperty("NamespaceInfo", value)
}

// GetNamespaceConfig gets a NamespaceDescribeReply's Config field
// from its properties map. NamespaceConfig is the namespace.NamespaceConfig to set in the activity
// complete call.
//
// returns *namespace.NamespaceConfig
func (reply *NamespaceDescribeReply) GetNamespaceConfig() *namespace.NamespaceConfig {
	config := new(namespace.NamespaceConfig)
	err := reply.GetJSONProperty("NamespaceConfig", config)
	if err != nil {
		return nil
	}
	return config
}

// SetNamespaceConfig sets an NamespaceDescribeReply's Config field
// from its properties map.  NamespaceConfig is the namespace.NamespaceConfig
//
// param value namespace.NamespaceConfig
func (reply *NamespaceDescribeReply) SetNamespaceConfig(value *namespace.NamespaceConfig) {
	reply.SetJSONProperty("NamespaceConfig", value)
}

// GetNamespaceReplicationConfig gets a NamespaceDescribeReply's Config field
// from its properties map.
//
// returns *replication.NamespaceReplicationConfig
func (reply *NamespaceDescribeReply) GetNamespaceReplicationConfig() *replication.NamespaceReplicationConfig {
	config := new(replication.NamespaceReplicationConfig)
	err := reply.GetJSONProperty("NamespaceReplicationConfig", config)
	if err != nil {
		return nil
	}
	return config
}

// SetNamespaceReplicationConfig sets an NamespaceDescribeReply's Config field
// from its properties map.
//
// param value *replication.NamespaceReplicationConfig
func (reply *NamespaceDescribeReply) SetNamespaceReplicationConfig(value *replication.NamespaceReplicationConfig) {
	reply.SetJSONProperty("NamespaceReplicationConfig", value)
}

// GetFailoverVersion gets a NamespaceDescribeReply's failover version
// from its properties map.
//
// returns int64 failover version.
func (reply *NamespaceDescribeReply) GetFailoverVersion() int64 {
	return reply.GetLongProperty("FailoverVersion")
}

// SetFailoverVersion gets a NamespaceDescribeReply's failover version
// from its properties map.
//
// returns int64 failover version.
func (reply *NamespaceDescribeReply) SetFailoverVersion(value int64) {
	reply.SetLongProperty("FailoverVersion", value)
}

// GetIsGlobalNamespace gets a NamespaceDescribeReply's global namespace flag
// from its properties map.
//
// returns bool global namespace flag.
func (reply *NamespaceDescribeReply) GetIsGlobalNamespace() bool {
	return reply.GetBoolProperty("IsGlobalNamespace")
}

// SetIsGlobalNamespace gets a NamespaceDescribeReply's global namespace flag
// from its properties map.
//
// returns bool global namespace flag.
func (reply *NamespaceDescribeReply) SetIsGlobalNamespace(value bool) {
	reply.SetBoolProperty("IsGlobalNamespace", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Build inherits docs from ProxyReply.Build()
func (reply *NamespaceDescribeReply) Build(e error, result ...interface{}) {
	reply.ProxyReply.Build(e)
	if len(result) > 0 {
		if v, ok := result[0].(*workflowservice.DescribeNamespaceResponse); ok {
			reply.SetNamespaceInfo(v.NamespaceInfo)
			reply.SetNamespaceConfig(v.Config)
			reply.SetNamespaceReplicationConfig(v.ReplicationConfig)
			reply.SetFailoverVersion(v.FailoverVersion)
			reply.SetIsGlobalNamespace(v.IsGlobalNamespace)
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
		v.SetNamespaceInfo(reply.GetNamespaceInfo())
		v.SetNamespaceConfig(reply.GetNamespaceConfig())
		v.SetNamespaceReplicationConfig(reply.GetNamespaceReplicationConfig())
		v.SetFailoverVersion(reply.GetFailoverVersion())
		v.SetIsGlobalNamespace(reply.GetIsGlobalNamespace())
	}
}

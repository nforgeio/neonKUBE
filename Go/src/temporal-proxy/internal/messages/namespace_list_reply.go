//-----------------------------------------------------------------------------
// FILE:		namespace_List_reply.go
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

	"go.temporal.io/temporal-proto/workflowservice"
)

type (
	// NamespaceListReply is a ProxyReply of MessageType
	// NamespaceListReply.  It holds a reference to a ProxyReply in memory
	NamespaceListReply struct {
		*ProxyReply
	}
)

// NewNamespaceListReply is the default constructor for
// a NamespaceListReply
//
// returns *NamespaceListReply -> a pointer to a newly initialized
// NamespaceListReply in memory
func NewNamespaceListReply() *NamespaceListReply {
	reply := new(NamespaceListReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(internal.NamespaceListReply)

	return reply
}

// GetNamespaces gets the Namespaces property as a string
// pointer from a NamespaceListReply's properties map,
// lists information about the Temporal namespaces.
//
// returns []*workflowservice.DescribeNamespaceResponse -> Lists information about the
// Temporal namespaces.
func (reply *NamespaceListReply) GetNamespaces() []*workflowservice.DescribeNamespaceResponse {
	var resp []*workflowservice.DescribeNamespaceResponse
	err := reply.GetJSONProperty("Namespaces", &resp)
	if err != nil {
		return nil
	}

	return resp
}

// SetNamespaces sets the Namespaces property as a string
// pointer in a NamespaceListReply's properties map,
// lists information about the Temporal namespaces.
//
// param value []*workflowservice.DescribeNamespaceResponse -> Lists information about the
// Temporal namespaces.
func (reply *NamespaceListReply) SetNamespaces(value []*workflowservice.DescribeNamespaceResponse) {
	reply.SetJSONProperty("Namespaces", value)
}

// GetNextPageToken gets a NamespaceListReply's NextPageToken value
// from its properties map, optionally specifies the next page of results.
// This will be null for the first page of results and can be set to the the value returned
// as NamespaceListReply.NextPageToken to retrieve the next page
// of results.  This should be considered to be an opaque value.
//
// returns []byte -> []byte next page token.
func (reply *NamespaceListReply) GetNextPageToken() []byte {
	return reply.GetBytesProperty("NextPageToken")
}

// SetNextPageToken sets a NamespaceListReply's NextPageToken value
// in its properties map, optionally specifies the next page of results.
// This will be null for the first page of results and can be set to the the value returned
// as NamespaceListReply.NextPageToken to retrieve the next page
// of results.  This should be considered to be an opaque value.
//
// param value []byte -> []byte next page token.
func (reply *NamespaceListReply) SetNextPageToken(value []byte) {
	reply.SetBytesProperty("NextPageToken", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *NamespaceListReply) Clone() IProxyMessage {
	namespaceListReply := NewNamespaceListReply()
	var messageClone IProxyMessage = namespaceListReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *NamespaceListReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(*NamespaceListReply); ok {
		v.SetNamespaces(reply.GetNamespaces())
		v.SetNextPageToken(reply.GetNextPageToken())
	}
}

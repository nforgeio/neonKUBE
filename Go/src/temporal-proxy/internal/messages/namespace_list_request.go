//-----------------------------------------------------------------------------
// FILE:		namespace_list_request.go
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

	// NamespaceListRequest is ProxyRequest of MessageType
	// NamespaceListRequest.
	//
	// A NamespaceListRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest.
	//
	// Requests a list of the Temporal namespaces.
	NamespaceListRequest struct {
		*ProxyRequest
	}
)

// NewNamespaceListRequest is the default constructor for a NamespaceListRequest
//
// returns *NamespaceListRequest -> a reference to a newly initialized
// NamespaceListRequest in memory
func NewNamespaceListRequest() *NamespaceListRequest {
	request := new(NamespaceListRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.NamespaceListRequest)
	request.SetReplyType(internal.NamespaceListReply)

	return request
}

// GetPageSize gets a NamespaceListRequest's PageSize value
// from its properties map, specifies the maximum number
// of items to be returned in the reponse.
//
// returns int32 -> int32 page size.
func (request *NamespaceListRequest) GetPageSize() int32 {
	return request.GetIntProperty("PageSize")
}

// SetPageSize sets a NamespaceListRequest's PageSize value
// in its properties map, specifies the maximum number
// of items to be returned in the reponse.
//
// param value int32 -> int32 page size.
func (request *NamespaceListRequest) SetPageSize(value int32) {
	request.SetIntProperty("PageSize", value)
}

// GetNextPageToken gets a NamespaceListRequest's NextPageToken value
// from its properties map, optionally specifies the next page of results.
// This will be null for the first page of results and can be set to the the value returned
// as NamespaceListReply.NextPageToken to retrieve the next page
// of results.  This should be considered to be an opaque value.
//
// returns []byte -> []byte next page token.
func (request *NamespaceListRequest) GetNextPageToken() []byte {
	return request.GetBytesProperty("NextPageToken")
}

// SetNextPageToken sets a NamespaceListRequest's NextPageToken value
// in its properties map, optionally specifies the next page of results.
// This will be null for the first page of results and can be set to the the value returned
// as NamespaceListReply.NextPageToken to retrieve the next page
// of results.  This should be considered to be an opaque value.
//
// param value []byte -> []byte next page token.
func (request *NamespaceListRequest) SetNextPageToken(value []byte) {
	request.SetBytesProperty("NextPageToken", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *NamespaceListRequest) Clone() IProxyMessage {
	namespaceListRequest := NewNamespaceListRequest()
	var messageClone IProxyMessage = namespaceListRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *NamespaceListRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*NamespaceListRequest); ok {
		v.SetPageSize(request.GetPageSize())
		v.SetNextPageToken(request.GetNextPageToken())
	}
}

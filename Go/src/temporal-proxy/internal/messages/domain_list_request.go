//-----------------------------------------------------------------------------
// FILE:		domain_list_request.go
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

	// DomainListRequest is ProxyRequest of MessageType
	// DomainListRequest.
	//
	// A DomainListRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest.
	//
	// Requests a list of the Temporal domains.
	DomainListRequest struct {
		*ProxyRequest
	}
)

// NewDomainListRequest is the default constructor for a DomainListRequest
//
// returns *DomainListRequest -> a reference to a newly initialized
// DomainListRequest in memory
func NewDomainListRequest() *DomainListRequest {
	request := new(DomainListRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(internal.DomainListRequest)
	request.SetReplyType(internal.DomainListReply)

	return request
}

// GetPageSize gets a DomainListRequest's PageSize value
// from its properties map, specifies the maximum number
// of items to be returned in the reponse.
//
// returns int32 -> int32 page size.
func (request *DomainListRequest) GetPageSize() int32 {
	return request.GetIntProperty("PageSize")
}

// SetPageSize sets a DomainListRequest's PageSize value
// in its properties map, specifies the maximum number
// of items to be returned in the reponse.
//
// param value int32 -> int32 page size.
func (request *DomainListRequest) SetPageSize(value int32) {
	request.SetIntProperty("PageSize", value)
}

// GetNextPageToken gets a DomainListRequest's NextPageToken value
// from its properties map, optionally specifies the next page of results.
// This will be null for the first page of results and can be set to the the value returned
// as DomainListReply.NextPageToken to retrieve the next page
// of results.  This should be considered to be an opaque value.
//
// returns []byte -> []byte next page token.
func (request *DomainListRequest) GetNextPageToken() []byte {
	return request.GetBytesProperty("NextPageToken")
}

// SetNextPageToken sets a DomainListRequest's NextPageToken value
// in its properties map, optionally specifies the next page of results.
// This will be null for the first page of results and can be set to the the value returned
// as DomainListReply.NextPageToken to retrieve the next page
// of results.  This should be considered to be an opaque value.
//
// param value []byte -> []byte next page token.
func (request *DomainListRequest) SetNextPageToken(value []byte) {
	request.SetBytesProperty("NextPageToken", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *DomainListRequest) Clone() IProxyMessage {
	domainListRequest := NewDomainListRequest()
	var messageClone IProxyMessage = domainListRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *DomainListRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*DomainListRequest); ok {
		v.SetPageSize(request.GetPageSize())
		v.SetNextPageToken(request.GetNextPageToken())
	}
}

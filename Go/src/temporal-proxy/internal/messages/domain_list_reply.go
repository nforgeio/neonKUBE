//-----------------------------------------------------------------------------
// FILE:		domain_List_reply.go
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
	//temporalshared "go.temporal.io/temporal/.gen/go/shared"
)

type (
	// DomainListReply is a ProxyReply of MessageType
	// DomainListReply.  It holds a reference to a ProxyReply in memory
	DomainListReply struct {
		*ProxyReply
	}
)

// NewDomainListReply is the default constructor for
// a DomainListReply
//
// returns *DomainListReply -> a pointer to a newly initialized
// DomainListReply in memory
func NewDomainListReply() *DomainListReply {
	reply := new(DomainListReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(internal.DomainListReply)

	return reply
}

// GetDomains gets the Domains property as a string
// pointer from a DomainListReply's properties map,
// lists information about the Temporal domains.
//
// returns []*temporalshared.DescribeDomainResponse -> Lists information about the
// Temporal domains.
func (reply *DomainListReply) GetDomains() []*temporalshared.DescribeDomainResponse {
	var resp []*temporalshared.DescribeDomainResponse
	err := reply.GetJSONProperty("Domains", &resp)
	if err != nil {
		return nil
	}

	return resp
}

// SetDomains sets the Domains property as a string
// pointer in a DomainListReply's properties map,
// lists information about the Temporal domains.
//
// param value []*temporalshared.DescribeDomainResponse -> Lists information about the
// Temporal domains.
func (reply *DomainListReply) SetDomains(value []*temporalshared.DescribeDomainResponse) {
	reply.SetJSONProperty("Domains", value)
}

// GetNextPageToken gets a DomainListReply's NextPageToken value
// from its properties map, optionally specifies the next page of results.
// This will be null for the first page of results and can be set to the the value returned
// as DomainListReply.NextPageToken to retrieve the next page
// of results.  This should be considered to be an opaque value.
//
// returns []byte -> []byte next page token.
func (reply *DomainListReply) GetNextPageToken() []byte {
	return reply.GetBytesProperty("NextPageToken")
}

// SetNextPageToken sets a DomainListReply's NextPageToken value
// in its properties map, optionally specifies the next page of results.
// This will be null for the first page of results and can be set to the the value returned
// as DomainListReply.NextPageToken to retrieve the next page
// of results.  This should be considered to be an opaque value.
//
// param value []byte -> []byte next page token.
func (reply *DomainListReply) SetNextPageToken(value []byte) {
	reply.SetBytesProperty("NextPageToken", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *DomainListReply) Clone() IProxyMessage {
	domainListReply := NewDomainListReply()
	var messageClone IProxyMessage = domainListReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *DomainListReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(*DomainListReply); ok {
		v.SetDomains(reply.GetDomains())
		v.SetNextPageToken(reply.GetNextPageToken())
	}
}

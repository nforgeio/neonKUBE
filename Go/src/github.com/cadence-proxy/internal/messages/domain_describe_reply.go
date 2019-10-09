//-----------------------------------------------------------------------------
// FILE:		domain_describe_reply.go
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
	proxyclient "github.com/cadence-proxy/internal/cadence/client"
	internal "github.com/cadence-proxy/internal"
)

type (

	// DomainDescribeReply is a ProxyReply of MessageType
	// DomainDescribeReply.  It holds a reference to a ProxyReply in memory
	DomainDescribeReply struct {
		*ProxyReply
	}
)

// NewDomainDescribeReply is the default constructor for
// a DomainDescribeReply
//
// returns *DomainDescribeReply -> a pointer to a newly initialized
// DomainDescribeReply in memory
func NewDomainDescribeReply() *DomainDescribeReply {
	reply := new(DomainDescribeReply)
	reply.ProxyReply = NewProxyReply()
	reply.SetType(internal.DomainDescribeReply)

	return reply
}

// GetDomainInfoName gets the DomainInfoName property as a string
// pointer from a DomainDescribeReply's properties map
//
// returns *string -> pointer to a string from a DomainDescribeReply's properties map
func (reply *DomainDescribeReply) GetDomainInfoName() *string {
	return reply.GetStringProperty("DomainInfoName")
}

// SetDomainInfoName sets the DomainInfoName property as a string
// pointer in a DomainDescribeReply's properties map
//
// param value *string -> pointer to the string value to set
// as the DomainDescribeReply's DomainInfoName in its properties map
func (reply *DomainDescribeReply) SetDomainInfoName(value *string) {
	reply.SetStringProperty("DomainInfoName", value)
}

// GetDomainInfoDescription gets the DomainInfoDescription property as a string
// pointer from a DomainDescribeReply's properties map
//
// returns *string -> pointer to a string from a DomainDescribeReply's properties map
func (reply *DomainDescribeReply) GetDomainInfoDescription() *string {
	return reply.GetStringProperty("DomainInfoDescription")
}

// SetDomainInfoDescription sets the DomainInfoDescription property as a string
// pointer in a DomainDescribeReply's properties map
//
// param value *string -> pointer to the string value to set
// as the DomainDescribeReply's DomainInfoDescription in its properties map
func (reply *DomainDescribeReply) SetDomainInfoDescription(value *string) {
	reply.SetStringProperty("DomainInfoDescription", value)
}

// GetDomainInfoStatus gets the DomainInfoStatus property as a string
// pointer from a DomainDescribeReply's properties map
//
// returns *DomainStatus -> DomainStatus of the Domain being described
// from a DomainDescribeReply's properties map
func (reply *DomainDescribeReply) GetDomainInfoStatus() proxyclient.DomainStatus {
	domainInfoStatusPtr := reply.GetStringProperty("DomainInfoStatus")
	if domainInfoStatusPtr == nil {
		return proxyclient.DomainStatusUnspecified
	}
	domainStatus := proxyclient.StringToDomainStatus(*domainInfoStatusPtr)

	return domainStatus
}

// SetDomainInfoStatus sets the DomainInfoStatus property as a string
// pointer in a DomainDescribeReply's properties map
//
// param value *DomainStatus -> DomainStatus value to set
// as the DomainDescribeReply's DomainInfoStatus in its properties map
func (reply *DomainDescribeReply) SetDomainInfoStatus(value proxyclient.DomainStatus) {
	status := value.String()
	reply.SetStringProperty("DomainInfoStatus", &status)
}

// GetDomainInfoOwnerEmail gets the DomainInfoOwnerEmail property as a string
// pointer from a DomainDescribeReply's properties map
//
// returns *string -> pointer to a string from a DomainDescribeReply's properties map
func (reply *DomainDescribeReply) GetDomainInfoOwnerEmail() *string {
	return reply.GetStringProperty("DomainInfoOwnerEmail")
}

// SetDomainInfoOwnerEmail sets the DomainInfoOwnerEmail property as a string
// pointer in a DomainDescribeReply's properties map
//
// param value *string -> pointer to the string value to set
// as the DomainDescribeReply's DomainInfoOwnerEmail in its properties map
func (reply *DomainDescribeReply) SetDomainInfoOwnerEmail(value *string) {
	reply.SetStringProperty("DomainInfoOwnerEmail", value)
}

// GetConfigurationRetentionDays gets the ConfigurationRetentionDays property
// as an int32 from a DomainDescribeReply's properties map
//
// returns int32 -> int32 domain retention in days value from a DomainDescribeReply's
// properties map
func (reply *DomainDescribeReply) GetConfigurationRetentionDays() int32 {
	return reply.GetIntProperty("ConfigurationRetentionDays")
}

// SetConfigurationRetentionDays sets the ConfigurationRetentionDays property as
// an int32 pointer in a DomainDescribeReply's properties map
//
// param value int32 -> int32 domain retention in days to set
// as the DomainDescribeReply's ConfigurationRetentionDays in its properties map
func (reply *DomainDescribeReply) SetConfigurationRetentionDays(value int32) {
	reply.SetIntProperty("ConfigurationRetentionDays", value)
}

// GetConfigurationEmitMetrics gets the ConfigurationEmitMetrics property
// as a bool from a DomainDescribeReply's properties map
//
// returns bool -> bool ConfigurationEmitMetrics value from a DomainDescribeReply's
// properties map
func (reply *DomainDescribeReply) GetConfigurationEmitMetrics() bool {
	return reply.GetBoolProperty("ConfigurationEmitMetrics")
}

// SetConfigurationEmitMetrics sets the ConfigurationEmitMetrics property as
// a bool in a DomainDescribeReply's properties map
//
// param value bool -> bool that enables metric generation
// as the DomainDescribeReply's ConfigurationEmitMetrics in its properties map
func (reply *DomainDescribeReply) SetConfigurationEmitMetrics(value bool) {
	reply.SetBoolProperty("ConfigurationEmitMetrics", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyReply.Clone()
func (reply *DomainDescribeReply) Clone() IProxyMessage {
	domainDescribeReply := NewDomainDescribeReply()
	var messageClone IProxyMessage = domainDescribeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyReply.CopyTo()
func (reply *DomainDescribeReply) CopyTo(target IProxyMessage) {
	reply.ProxyReply.CopyTo(target)
	if v, ok := target.(*DomainDescribeReply); ok {
		v.SetDomainInfoName(reply.GetDomainInfoName())
		v.SetConfigurationEmitMetrics(reply.GetConfigurationEmitMetrics())
		v.SetConfigurationRetentionDays(reply.GetConfigurationRetentionDays())
		v.SetDomainInfoDescription(reply.GetDomainInfoDescription())
		v.SetDomainInfoStatus(reply.GetDomainInfoStatus())
		v.SetDomainInfoOwnerEmail(reply.GetDomainInfoOwnerEmail())
	}
}

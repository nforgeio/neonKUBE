package types

import (
	"github.com/loopieio/cadence-proxy/internal/messages"
)

type (

	// DomainUpdateRequest is ProxyRequest of MessageType
	// DomainUpdateRequest.
	//
	// A DomainUpdateRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	DomainUpdateRequest struct {
		*ProxyRequest
	}
)

// NewDomainUpdateRequest is the default constructor for a DomainUpdateRequest
//
// returns *DomainUpdateRequest -> a reference to a newly initialized
// DomainUpdateRequest in memory
func NewDomainUpdateRequest() *DomainUpdateRequest {
	request := new(DomainUpdateRequest)
	request.ProxyRequest = NewProxyRequest()
	request.Type = messages.DomainUpdateRequest
	request.SetReplyType(messages.DomainUpdateReply)

	return request
}

// GetName gets a DomainUpdateRequest's Name value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a DomainUpdateRequest's Name
func (request *DomainUpdateRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a DomainUpdateRequest's Name value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *DomainUpdateRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// GetUpdatedInfoDescription gets a DomainUpdateRequest's UpdatedInfoDescription
// value from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a DomainUpdateRequest's UpdatedInfoDescription
func (request *DomainUpdateRequest) GetUpdatedInfoDescription() *string {
	return request.GetStringProperty("UpdatedInfoDescription")
}

// SetUpdatedInfoDescription sets a DomainUpdateRequest's UpdatedInfoDescription
// value in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *DomainUpdateRequest) SetUpdatedInfoDescription(value *string) {
	request.SetStringProperty("UpdatedInfoDescription", value)
}

// GetUpdatedInfoOwnerEmail gets a DomainUpdateRequest's UpdatedInfoOwnerEmail
// value from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a DomainUpdateRequest's UpdatedInfoOwnerEmail
func (request *DomainUpdateRequest) GetUpdatedInfoOwnerEmail() *string {
	return request.GetStringProperty("UpdatedInfoOwnerEmail")
}

// SetUpdatedInfoOwnerEmail sets a DomainUpdateRequest's UpdatedInfoOwnerEmail
// value in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *DomainUpdateRequest) SetUpdatedInfoOwnerEmail(value *string) {
	request.SetStringProperty("UpdatedInfoOwnerEmail", value)
}

// GetConfigurationEmitMetrics gets a DomainUpdateRequest's ConfigurationEmitMetrics
// value from its properties map
//
// returns bool -> bool specifying the metrics emission settings
func (request *DomainUpdateRequest) GetConfigurationEmitMetrics() bool {
	return request.GetBoolProperty("ConfigurationEmitMetrics")
}

// SetConfigurationEmitMetrics sets a DomainUpdateRequest's ConfigurationEmitMetrics
// value in its properties map
//
// param value bool -> bool value to be set in the properties map
func (request *DomainUpdateRequest) SetConfigurationEmitMetrics(value bool) {
	request.SetBoolProperty("ConfigurationEmitMetrics", value)
}

// GetConfigurationRetentionDays gets a DomainUpdateRequest's ConfigurationRetentionDays
// value from its properties map
//
// returns int32 -> int32 indicating the complete workflow history retention
// period in days
func (request *DomainUpdateRequest) GetConfigurationRetentionDays() int32 {
	return request.GetIntProperty("ConfigurationRetentionDays")
}

// SetConfigurationRetentionDays sets a DomainUpdateRequest's ConfigurationRetentionDays
// value in its properties map
//
// param value int32 -> int32 value to be set in the properties map
func (request *DomainUpdateRequest) SetConfigurationRetentionDays(value int32) {
	request.SetIntProperty("ConfigurationRetentionDays", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyMessage.Clone()
func (request *DomainUpdateRequest) Clone() IProxyMessage {
	domainUpdateRequest := NewDomainUpdateRequest()
	var messageClone IProxyMessage = domainUpdateRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
func (request *DomainUpdateRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*DomainUpdateRequest); ok {
		v.SetName(request.GetName())
		v.SetUpdatedInfoDescription(request.GetUpdatedInfoDescription())
		v.SetUpdatedInfoOwnerEmail(request.GetUpdatedInfoOwnerEmail())
		v.SetConfigurationEmitMetrics(request.GetConfigurationEmitMetrics())
		v.SetConfigurationRetentionDays(request.GetConfigurationRetentionDays())
	}
}

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (request *DomainUpdateRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyMessage.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (request *DomainUpdateRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyMessage.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (request *DomainUpdateRequest) GetRequestID() int64 {
	return request.ProxyMessage.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (request *DomainUpdateRequest) SetRequestID(value int64) {
	request.ProxyMessage.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *DomainUpdateRequest) GetReplyType() messages.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *DomainUpdateRequest) SetReplyType(value messages.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}

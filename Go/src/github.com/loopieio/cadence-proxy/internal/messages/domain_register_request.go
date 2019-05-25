package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// DomainRegisterRequest is ProxyRequest of MessageType
	// DomainRegisterRequest.
	//
	// A DomainRegisterRequest contains a RequestId and a reference to a
	// ProxyReply struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ProxyRequest
	DomainRegisterRequest struct {
		*ProxyRequest
	}
)

// NewDomainRegisterRequest is the default constructor for a DomainRegisterRequest
//
// returns *DomainRegisterRequest -> a reference to a newly initialized
// DomainRegisterRequest in memory
func NewDomainRegisterRequest() *DomainRegisterRequest {
	request := new(DomainRegisterRequest)
	request.ProxyRequest = NewProxyRequest()
	request.SetType(messagetypes.DomainRegisterRequest)
	request.SetReplyType(messagetypes.DomainRegisterReply)

	return request
}

// GetName gets a DomainRegisterRequest's Name value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a DomainRegisterRequest's Name
func (request *DomainRegisterRequest) GetName() *string {
	return request.GetStringProperty("Name")
}

// SetName sets a DomainRegisterRequest's Name value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *DomainRegisterRequest) SetName(value *string) {
	request.SetStringProperty("Name", value)
}

// GetDescription gets a DomainRegisterRequest's Description value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a DomainRegisterRequest's Description
func (request *DomainRegisterRequest) GetDescription() *string {
	return request.GetStringProperty("Description")
}

// SetDescription sets a DomainRegisterRequest's Description value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *DomainRegisterRequest) SetDescription(value *string) {
	request.SetStringProperty("Description", value)
}

// GetOwnerEmail gets a DomainRegisterRequest's OwnerEmail value
// from its properties map
//
// returns *string -> pointer to a string in memory holding the value
// of a DomainRegisterRequest's OwnerEmail
func (request *DomainRegisterRequest) GetOwnerEmail() *string {
	return request.GetStringProperty("OwnerEmail")
}

// SetOwnerEmail sets a DomainRegisterRequest's OwnerEmail value
// in its properties map
//
// param value *string -> a pointer to a string in memory that holds the value
// to be set in the properties map
func (request *DomainRegisterRequest) SetOwnerEmail(value *string) {
	request.SetStringProperty("OwnerEmail", value)
}

// GetEmitMetrics gets a DomainRegisterRequest's EmitMetrics value
// from its properties map
//
// returns bool -> bool indicating whether or not to enable metrics
func (request *DomainRegisterRequest) GetEmitMetrics() bool {
	return request.GetBoolProperty("EmitMetrics")
}

// SetEmitMetrics sets a DomainRegisterRequest's EmitMetrics value
// in its properties map
//
// param value bool -> bool value to be set in the properties map
func (request *DomainRegisterRequest) SetEmitMetrics(value bool) {
	request.SetBoolProperty("EmitMetrics", value)
}

// GetRetentionDays gets a DomainRegisterRequest's RetentionDays value
// from its properties map
//
// returns int32 -> int32 indicating the complete workflow history retention
// period in days
func (request *DomainRegisterRequest) GetRetentionDays() int32 {
	return request.GetIntProperty("RetentionDays")
}

// SetRetentionDays sets a DomainRegisterRequest's EmitMetrics value
// in its properties map
//
// param value int32 -> int32 value to be set in the properties map
func (request *DomainRegisterRequest) SetRetentionDays(value int32) {
	request.SetIntProperty("RetentionDays", value)
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ProxyRequest.Clone()
func (request *DomainRegisterRequest) Clone() IProxyMessage {
	domainRegisterRequest := NewDomainRegisterRequest()
	var messageClone IProxyMessage = domainRegisterRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyRequest.CopyTo()
func (request *DomainRegisterRequest) CopyTo(target IProxyMessage) {
	request.ProxyRequest.CopyTo(target)
	if v, ok := target.(*DomainRegisterRequest); ok {
		v.SetName(request.GetName())
		v.SetDescription(request.GetDescription())
		v.SetOwnerEmail(request.GetOwnerEmail())
		v.SetEmitMetrics(request.GetEmitMetrics())
		v.SetRetentionDays(request.GetRetentionDays())
	}
}

// SetProxyMessage inherits docs from ProxyRequest.SetProxyMessage()
func (request *DomainRegisterRequest) SetProxyMessage(value *ProxyMessage) {
	request.ProxyRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyRequest.GetProxyMessage()
func (request *DomainRegisterRequest) GetProxyMessage() *ProxyMessage {
	return request.ProxyRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyRequest.GetRequestID()
func (request *DomainRegisterRequest) GetRequestID() int64 {
	return request.ProxyRequest.GetRequestID()
}

// SetRequestID inherits docs from ProxyRequest.SetRequestID()
func (request *DomainRegisterRequest) SetRequestID(value int64) {
	request.ProxyRequest.SetRequestID(value)
}

// GetType inherits docs from ProxyRequest.GetType()
func (request *DomainRegisterRequest) GetType() messagetypes.MessageType {
	return request.ProxyRequest.GetType()
}

// SetType inherits docs from ProxyRequest.SetType()
func (request *DomainRegisterRequest) SetType(value messagetypes.MessageType) {
	request.ProxyRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ProxyRequest.GetReplyType()
func (request *DomainRegisterRequest) GetReplyType() messagetypes.MessageType {
	return request.ProxyRequest.GetReplyType()
}

// SetReplyType inherits docs from ProxyRequest.SetReplyType()
func (request *DomainRegisterRequest) SetReplyType(value messagetypes.MessageType) {
	request.ProxyRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ProxyRequest.GetTimeout()
func (request *DomainRegisterRequest) GetTimeout() time.Duration {
	return request.ProxyRequest.GetTimeout()
}

// SetTimeout inherits docs from ProxyRequest.SetTimeout()
func (request *DomainRegisterRequest) SetTimeout(value time.Duration) {
	request.ProxyRequest.SetTimeout(value)
}

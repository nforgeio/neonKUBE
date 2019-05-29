package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityGetHeartbeatDetailsRequest is an ActivityRequest of MessageType
	// ActivityGetHeartbeatDetailsRequest.
	//
	// A ActivityGetHeartbeatDetailsRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Requests the details for the last heartbeat
	// recorded for a failed previous run of the activity.
	ActivityGetHeartbeatDetailsRequest struct {
		*ActivityRequest
	}
)

// NewActivityGetHeartbeatDetailsRequest is the default constructor for a ActivityGetHeartbeatDetailsRequest
//
// returns *ActivityGetHeartbeatDetailsRequest -> a pointer to a newly initialized ActivityGetHeartbeatDetailsRequest
// in memory
func NewActivityGetHeartbeatDetailsRequest() *ActivityGetHeartbeatDetailsRequest {
	request := new(ActivityGetHeartbeatDetailsRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(messagetypes.ActivityGetHeartbeatDetailsRequest)
	request.SetReplyType(messagetypes.ActivityGetHeartbeatDetailsReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityGetHeartbeatDetailsRequest) Clone() IProxyMessage {
	activityGetHeartbeatDetailsRequest := NewActivityGetHeartbeatDetailsRequest()
	var messageClone IProxyMessage = activityGetHeartbeatDetailsRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityGetHeartbeatDetailsRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
}

// SetProxyMessage inherits docs from ActivityRequest.SetProxyMessage()
func (request *ActivityGetHeartbeatDetailsRequest) SetProxyMessage(value *ProxyMessage) {
	request.ActivityRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ActivityRequest.GetProxyMessage()
func (request *ActivityGetHeartbeatDetailsRequest) GetProxyMessage() *ProxyMessage {
	return request.ActivityRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ActivityRequest.GetRequestID()
func (request *ActivityGetHeartbeatDetailsRequest) GetRequestID() int64 {
	return request.ActivityRequest.GetRequestID()
}

// SetRequestID inherits docs from ActivityRequest.SetRequestID()
func (request *ActivityGetHeartbeatDetailsRequest) SetRequestID(value int64) {
	request.ActivityRequest.SetRequestID(value)
}

// GetType inherits docs from ActivityRequest.GetType()
func (request *ActivityGetHeartbeatDetailsRequest) GetType() messagetypes.MessageType {
	return request.ActivityRequest.GetType()
}

// SetType inherits docs from ActivityRequest.SetType()
func (request *ActivityGetHeartbeatDetailsRequest) SetType(value messagetypes.MessageType) {
	request.ActivityRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ActivityRequest.GetReplyType()
func (request *ActivityGetHeartbeatDetailsRequest) GetReplyType() messagetypes.MessageType {
	return request.ActivityRequest.GetReplyType()
}

// SetReplyType inherits docs from ActivityRequest.SetReplyType()
func (request *ActivityGetHeartbeatDetailsRequest) SetReplyType(value messagetypes.MessageType) {
	request.ActivityRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ActivityRequest.GetTimeout()
func (request *ActivityGetHeartbeatDetailsRequest) GetTimeout() time.Duration {
	return request.ActivityRequest.GetTimeout()
}

// SetTimeout inherits docs from ActivityRequest.SetTimeout()
func (request *ActivityGetHeartbeatDetailsRequest) SetTimeout(value time.Duration) {
	request.ActivityRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IActivityRequest interface methods for implementing the IActivityRequest interface

// GetContextID inherits docs from ActivityRequest.GetContextID()
func (request *ActivityGetHeartbeatDetailsRequest) GetContextID() int64 {
	return request.ActivityRequest.GetContextID()
}

// SetContextID inherits docs from ActivityRequest.SetContextID()
func (request *ActivityGetHeartbeatDetailsRequest) SetContextID(value int64) {
	request.ActivityRequest.SetContextID(value)
}

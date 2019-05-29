package messages

import (
	"time"

	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
)

type (

	// ActivityHasHeartbeatDetailsRequest is an ActivityRequest of MessageType
	// ActivityHasHeartbeatDetailsRequest.
	//
	// A ActivityHasHeartbeatDetailsRequest contains a reference to a
	// ActivityRequest struct in memory and ReplyType, which is
	// the corresponding MessageType for replying to this ActivityRequest
	//
	// Determines whether a previous failed run on an
	// activity recorded heartbeat details.
	ActivityHasHeartbeatDetailsRequest struct {
		*ActivityRequest
	}
)

// NewActivityHasHeartbeatDetailsRequest is the default constructor for a ActivityHasHeartbeatDetailsRequest
//
// returns *ActivityHasHeartbeatDetailsRequest -> a pointer to a newly initialized ActivityHasHeartbeatDetailsRequest
// in memory
func NewActivityHasHeartbeatDetailsRequest() *ActivityHasHeartbeatDetailsRequest {
	request := new(ActivityHasHeartbeatDetailsRequest)
	request.ActivityRequest = NewActivityRequest()
	request.SetType(messagetypes.ActivityHasHeartbeatDetailsRequest)
	request.SetReplyType(messagetypes.ActivityHasHeartbeatDetailsReply)

	return request
}

// -------------------------------------------------------------------------
// IProxyMessage interface methods for implementing the IProxyMessage interface

// Clone inherits docs from ActivityRequest.Clone()
func (request *ActivityHasHeartbeatDetailsRequest) Clone() IProxyMessage {
	activityHasHeartbeatDetailsRequest := NewActivityHasHeartbeatDetailsRequest()
	var messageClone IProxyMessage = activityHasHeartbeatDetailsRequest
	request.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ActivityRequest.CopyTo()
func (request *ActivityHasHeartbeatDetailsRequest) CopyTo(target IProxyMessage) {
	request.ActivityRequest.CopyTo(target)
}

// SetProxyMessage inherits docs from ActivityRequest.SetProxyMessage()
func (request *ActivityHasHeartbeatDetailsRequest) SetProxyMessage(value *ProxyMessage) {
	request.ActivityRequest.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ActivityRequest.GetProxyMessage()
func (request *ActivityHasHeartbeatDetailsRequest) GetProxyMessage() *ProxyMessage {
	return request.ActivityRequest.GetProxyMessage()
}

// GetRequestID inherits docs from ActivityRequest.GetRequestID()
func (request *ActivityHasHeartbeatDetailsRequest) GetRequestID() int64 {
	return request.ActivityRequest.GetRequestID()
}

// SetRequestID inherits docs from ActivityRequest.SetRequestID()
func (request *ActivityHasHeartbeatDetailsRequest) SetRequestID(value int64) {
	request.ActivityRequest.SetRequestID(value)
}

// GetType inherits docs from ActivityRequest.GetType()
func (request *ActivityHasHeartbeatDetailsRequest) GetType() messagetypes.MessageType {
	return request.ActivityRequest.GetType()
}

// SetType inherits docs from ActivityRequest.SetType()
func (request *ActivityHasHeartbeatDetailsRequest) SetType(value messagetypes.MessageType) {
	request.ActivityRequest.SetType(value)
}

// -------------------------------------------------------------------------
// IProxyRequest interface methods for implementing the IProxyRequest interface

// GetReplyType inherits docs from ActivityRequest.GetReplyType()
func (request *ActivityHasHeartbeatDetailsRequest) GetReplyType() messagetypes.MessageType {
	return request.ActivityRequest.GetReplyType()
}

// SetReplyType inherits docs from ActivityRequest.SetReplyType()
func (request *ActivityHasHeartbeatDetailsRequest) SetReplyType(value messagetypes.MessageType) {
	request.ActivityRequest.SetReplyType(value)
}

// GetTimeout inherits docs from ActivityRequest.GetTimeout()
func (request *ActivityHasHeartbeatDetailsRequest) GetTimeout() time.Duration {
	return request.ActivityRequest.GetTimeout()
}

// SetTimeout inherits docs from ActivityRequest.SetTimeout()
func (request *ActivityHasHeartbeatDetailsRequest) SetTimeout(value time.Duration) {
	request.ActivityRequest.SetTimeout(value)
}

// -------------------------------------------------------------------------
// IActivityRequest interface methods for implementing the IActivityRequest interface

// GetContextID inherits docs from ActivityRequest.GetContextID()
func (request *ActivityHasHeartbeatDetailsRequest) GetContextID() int64 {
	return request.ActivityRequest.GetContextID()
}

// SetContextID inherits docs from ActivityRequest.SetContextID()
func (request *ActivityHasHeartbeatDetailsRequest) SetContextID(value int64) {
	request.ActivityRequest.SetContextID(value)
}

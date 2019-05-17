package messages

import (
	"errors"

	domain "github.com/loopieio/cadence-proxy/internal/cadence/cadencedomains"
	"github.com/loopieio/cadence-proxy/internal/cadence/cadenceerrors"
	messagetypes "github.com/loopieio/cadence-proxy/internal/messages/types"
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
	reply.Type = messagetypes.DomainDescribeReply

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
// returns *DomainStatus -> pointer to the DomainStatus of the Domain being described
// from a DomainDescribeReply's properties map
func (reply *DomainDescribeReply) GetDomainInfoStatus() *domain.DomainStatus {
	domainInfoStatusPtr := reply.GetStringProperty("DomainInfoStatus")
	if domainInfoStatusPtr == nil {
		return nil
	}

	// dereference and switch block on the value
	domainStatus := *domainInfoStatusPtr
	var returnStatus domain.DomainStatus
	switch domainStatus {
	case "REGISTERED":
		returnStatus = domain.Registered
	case "DEPRECATED":
		returnStatus = domain.Deprecated
	case "DELETED":
		returnStatus = domain.Deleted
	default:
		err := errors.New("domainStatus not implemented exception")
		panic(err)
	}

	return &returnStatus
}

// SetDomainInfoStatus sets the DomainInfoStatus property as a string
// pointer in a DomainDescribeReply's properties map
//
// param value *DomainStatus -> DomainStatus value to set
// as the DomainDescribeReply's DomainInfoStatus in its properties map
func (reply *DomainDescribeReply) SetDomainInfoStatus(value *domain.DomainStatus) {
	if value == nil {
		reply.SetStringProperty("DomainInfoStatus", nil)
		return
	}

	// switch block on the param value
	var statusString string
	switch *value {
	case domain.Registered:
		statusString = "REGISTERED"
	case domain.Deprecated:
		statusString = "DEPRECATED"
	case domain.Deleted:
		statusString = "DELETED"
	default:

		// panic if type is not recognized or implemented yet
		err := errors.New("not implemented exception")
		panic(err)
	}

	// set the string in the properties map
	reply.SetStringProperty("DomainInfoStatus", &statusString)
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

// Clone inherits docs from ProxyMessage.Clone()
func (reply *DomainDescribeReply) Clone() IProxyMessage {
	domainDescribeReply := NewDomainDescribeReply()
	var messageClone IProxyMessage = domainDescribeReply
	reply.CopyTo(messageClone)

	return messageClone
}

// CopyTo inherits docs from ProxyMessage.CopyTo()
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

// SetProxyMessage inherits docs from ProxyMessage.SetProxyMessage()
func (reply *DomainDescribeReply) SetProxyMessage(value *ProxyMessage) {
	reply.ProxyReply.SetProxyMessage(value)
}

// GetProxyMessage inherits docs from ProxyMessage.GetProxyMessage()
func (reply *DomainDescribeReply) GetProxyMessage() *ProxyMessage {
	return reply.ProxyReply.GetProxyMessage()
}

// GetRequestID inherits docs from ProxyMessage.GetRequestID()
func (reply *DomainDescribeReply) GetRequestID() int64 {
	return reply.ProxyReply.GetRequestID()
}

// SetRequestID inherits docs from ProxyMessage.SetRequestID()
func (reply *DomainDescribeReply) SetRequestID(value int64) {
	reply.ProxyReply.SetRequestID(value)
}

// -------------------------------------------------------------------------
// IProxyReply interface methods for implementing the IProxyReply interface

// GetError inherits docs from ProxyReply.GetError()
func (reply *DomainDescribeReply) GetError() *cadenceerrors.CadenceError {
	return reply.ProxyReply.GetError()
}

// SetError inherits docs from ProxyReply.SetError()
func (reply *DomainDescribeReply) SetError(value *cadenceerrors.CadenceError) {
	reply.ProxyReply.SetError(value)
}

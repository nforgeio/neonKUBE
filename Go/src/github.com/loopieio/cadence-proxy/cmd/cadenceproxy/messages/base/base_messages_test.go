package base_test

import (
	"bytes"
	"testing"
	"time"

	"github.com/a3linux/amazon-ssm-agent/agent/times"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/cadenceerrors"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/cluster"

	"github.com/stretchr/testify/suite"
)

type (
	UnitTestSuite struct {
		suite.Suite
	}
)

// --------------------------------------------------------------------------
// Test suite methods.  Set up the test suite and entrypoint for test suite

func TestUnitTestSuite(t *testing.T) {

	// setup the suite
	s := new(UnitTestSuite)
	s.setupTestSuiteMessagesMap()

	// run the tests
	suite.Run(t, s)
}

func (s *UnitTestSuite) setupTestSuiteMessagesMap() {
	cluster.FillMessageTypeStructMap()
}

// --------------------------------------------------------------------------
// Test the ProxyMessage helper methods

func (s *UnitTestSuite) TestPropertyHelpers() {

	// verify that the property helper methods work as expected
	message := base.NewProxyMessage()

	// verify that non-existant property values return the default for the requested type
	s.Nil(message.GetStringProperty("foo"))
	s.Equal(int32(0), message.GetIntProperty("foo"))
	s.Equal(int64(0), message.GetLongProperty("foo"))
	s.False(message.GetBoolProperty("foo"))
	s.Equal(0.0, message.GetDoubleProperty("foo"))
	s.Equal(times.ParseIso8601UTC(times.ToIso8601UTC(time.Time{})), message.GetDateTimeProperty("foo"))
	s.Equal(time.Duration(0)*time.Nanosecond, message.GetTimeSpanProperty("foo"))

	// Verify that we can override default values for non-existant properties.

	s.Equal(int32(123), message.GetIntProperty("foo", int32(123)))
	s.Equal(int64(456), message.GetLongProperty("foo", int64(456)))
	s.True(message.GetBoolProperty("foo", true))
	s.Equal(float64(123.456), message.GetDoubleProperty("foo", float64(123.456)))
	s.Equal(time.Date(2019, time.April, 14, 0, 0, 0, 0, time.UTC), message.GetDateTimeProperty("foo", time.Date(2019, time.April, 14, 0, 0, 0, 0, time.UTC)))
	s.Equal(time.Second*123, message.GetTimeSpanProperty("foo", time.Second*123))

	// verify that we can write and then read properties
	str := "bar"
	message.SetStringProperty("foo", &str)
	s.Equal("bar", *message.GetStringProperty("foo"))

	message.SetIntProperty("foo", int32(123))
	s.Equal(int32(123), message.GetIntProperty("foo"))

	message.SetLongProperty("foo", int64(456))
	s.Equal(int64(456), message.GetLongProperty("foo"))

	message.SetBoolProperty("foo", true)
	s.True(message.GetBoolProperty("foo"))

	message.SetDoubleProperty("foo", 123.456)
	s.Equal(123.456, message.GetDoubleProperty("foo"))

	date := time.Date(2019, time.April, 14, 0, 0, 0, 0, time.UTC)
	message.SetDateTimeProperty("foo", date)
	s.Equal(date, message.GetDateTimeProperty("foo"))

	message.SetTimeSpanProperty("foo", time.Second*123)
	s.Equal(time.Second*123, message.GetTimeSpanProperty("foo"))
}

// --------------------------------------------------------------------------
// Test the base messages (ProxyMessage, ProxyRequest, ProxyReply)

// TestProxyMessage ensures that we can
// serializate and deserialize a base ProxyMessage
func (s *UnitTestSuite) TestProxyMessage() {

	// empty buffer to create empty proxy message
	buf := bytes.NewBuffer(make([]byte, 0))
	message, err := base.Deserialize(buf, true)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*base.ProxyMessage); ok {
		s.Equal(messages.Unspecified, v.Type)
		s.Empty(v.Properties)
		s.Empty(v.Attachments)
	}

	// new proxy message to fill
	message = base.NewProxyMessage()

	if v, ok := message.(*base.ProxyMessage); ok {

		// fill the properties map
		p1 := "1"
		p2 := "2"
		p3 := ""
		v.Properties["One"] = &p1
		v.Properties["Two"] = &p2
		v.Properties["Empty"] = &p3
		v.Properties["Nil"] = nil

		// fill the attachments map
		v.Attachments = append(v.Attachments, []byte{0, 1, 2, 3, 4})
		v.Attachments = append(v.Attachments, make([]byte, 0))
		v.Attachments = append(v.Attachments, nil)

		// serialize the new message
		serializedMessage, err := v.Serialize(true)
		s.NoError(err)

		// byte buffer to deserialize
		buf = bytes.NewBuffer(serializedMessage)
	}

	// deserialize
	message, err = base.Deserialize(buf, true)
	s.NoError(err)

	// check that the values are the same
	if v, ok := message.(*base.ProxyMessage); ok {

		// type and property values
		s.Equal(messages.Unspecified, v.Type)
		s.Equal(4, len(v.Properties))
		s.Equal("1", *v.Properties["One"])
		s.Equal("2", *v.Properties["Two"])
		s.Empty(v.Properties["Empty"])
		s.Nil(v.Properties["Nil"])

		// attachment values
		s.Equal(3, len(v.Attachments))
		s.Equal([]byte{0, 1, 2, 3, 4}, v.Attachments[0])
		s.Empty(v.Attachments[1])
		s.Nil(v.Attachments[2])
	}
}
func (s *UnitTestSuite) TestProxyRequest() {

	// Ensure that we can serialize and deserialize request messages

	buf := bytes.NewBuffer(make([]byte, 0))
	message, err := base.Deserialize(buf, true, "ProxyRequest")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*base.ProxyRequest); ok {
		s.Equal(int64(0), v.GetRequestID())

		// Round-trip
		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		// serialize the new message
		serializedMessage, err := v.Serialize(true)
		s.NoError(err)

		// byte buffer to deserialize
		buf = bytes.NewBuffer(serializedMessage)
	}

	message, err = base.Deserialize(buf, true, "ProxyRequest")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*base.ProxyRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
	}
}

func (s *UnitTestSuite) TestProxyReply() {

	// Ensure that we can serialize and deserialize reply messages

	buf := bytes.NewBuffer(make([]byte, 0))
	message, err := base.Deserialize(buf, true, "ProxyReply")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*base.ProxyReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(cadenceerrors.None, v.GetErrorType())
		s.Nil(v.GetError())
		s.Nil(v.GetErrorDetails())

		// Round-trip
		v.SetRequestID(int64(555))
		v.SetErrorType(cadenceerrors.Custom)

		str1 := "MyError"
		str2 := "MyError Details"
		v.SetError(&str1)
		v.SetErrorDetails(&str2)

		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())

		// serialize the new message
		serializedMessage, err := v.Serialize(true)
		s.NoError(err)

		// byte buffer to deserialize
		buf = bytes.NewBuffer(serializedMessage)
	}

	message, err = base.Deserialize(buf, true, "ProxyReply")
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*base.ProxyReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(cadenceerrors.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
	}
}

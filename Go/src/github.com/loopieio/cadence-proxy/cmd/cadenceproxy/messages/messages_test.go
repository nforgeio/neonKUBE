package messages_test

import (
	"bytes"
	"fmt"
	"io/ioutil"
	"net/http"
	"os"
	"testing"
	"time"

	"github.com/a3linux/amazon-ssm-agent/agent/times"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/cluster"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/endpoints"
	"go.uber.org/zap"
	"go.uber.org/zap/zapcore"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/server"
	"github.com/stretchr/testify/suite"
)

type (
	UnitTestSuite struct {
		suite.Suite
		instance *server.Instance
	}
)

const (
	_listenAddress = "127.0.0.1:5000"
)

func TestUnitTestSuite(t *testing.T) {

	// setup the suite
	s := new(UnitTestSuite)
	s.setupTestSuiteMessagesMap()
	s.setupTestSuiteServer()

	// start the server as a go routine
	go s.instance.Start()

	// run the tests
	suite.Run(t, s)

	// send the server shutdown signal and wait
	// to exit until the server shuts down gracefully
	s.instance.ShutdownChannel <- true
	time.Sleep(time.Second * 1)
}

func (s *UnitTestSuite) setupTestSuiteServer() {

	// new *zap.Logger
	// new zapcore.EncoderConfig for the logger
	var logger *zap.Logger
	var encoderCfg zapcore.EncoderConfig

	// new AtomicLevel for dynamic logging level
	atom := zap.NewAtomicLevel()

	// set the log level
	atom.SetLevel(zap.DebugLevel)

	// set Debug in endpoints
	endpoints.Debug = true

	// create the logger
	encoderCfg = zap.NewDevelopmentEncoderConfig()
	encoderCfg.TimeKey = "Time"
	encoderCfg.LevelKey = "Level"
	encoderCfg.MessageKey = "Debug Message"
	logger = zap.New(zapcore.NewCore(
		zapcore.NewJSONEncoder(encoderCfg),
		zapcore.Lock(os.Stdout),
		atom,
	))
	defer logger.Sync()

	// set the global logger
	_ = zap.ReplaceGlobals(logger)

	// create the new server instance,
	// set the routes, and start the server listening
	// on host:port 127.0.0.1:5000
	s.instance = server.NewInstance(_listenAddress)
	endpoints.Instance = s.instance
	endpoints.SetupRoutes(s.instance.Router)
}

func (s *UnitTestSuite) setupTestSuiteMessagesMap() {
	base.InitProxyMessage()
	cluster.FillMessageTypeStructMap()
}

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
		s.Equal(messages.None, v.GetErrorType())
		s.Nil(v.GetError())
		s.Nil(v.GetErrorDetails())

		// Round-trip
		v.SetRequestID(int64(555))
		v.SetErrorType(messages.Custom)

		str1 := "MyError"
		str2 := "MyError Details"
		v.SetError(&str1)
		v.SetErrorDetails(&str2)

		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
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
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
	}
}

func (s *UnitTestSuite) echoToConnection(message base.IProxyMessage) (base.IProxyMessage, error) {
	proxyMessage := message.GetProxyMessage()
	content, err := proxyMessage.Serialize(false)
	if err != nil {
		return nil, err
	}

	buf := bytes.NewBuffer(content)
	address := fmt.Sprintf("http://%s/echo", _listenAddress)
	req, err := http.NewRequest(http.MethodPut, address, buf)
	if err != nil {
		return nil, err
	}

	// set the request header to specified content type
	req.Header.Set("Content-Type", endpoints.ContentType)

	// initialize the http.Client and send the request
	client := &http.Client{}
	resp, err := client.Do(req)
	if err != nil {
		return nil, err
	}
	defer resp.Body.Close()

	// create an empty []byte and read the
	// request body into it if not nil
	payload, err := ioutil.ReadAll(resp.Body)
	if err != nil {
		return nil, err
	}

	return base.Deserialize(bytes.NewBuffer(payload), false)
}

func (s *UnitTestSuite) TestInitializeRequest() {

	var message base.IProxyMessage = cluster.NewInitializeRequest()
	if v, ok := message.(*cluster.InitializeRequest); ok {
		s.Equal(messages.InitializeReply, v.GetReplyType())
	}

	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.InitializeRequest); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Nil(v.GetLibraryAddress())
		s.Equal(int32(0), v.GetLibraryPort())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())

		str := "1.2.3.4"
		v.SetLibraryAddress(&str)
		s.Equal("1.2.3.4", *v.GetLibraryAddress())
		v.SetLibraryPort(int32(666))
		s.Equal(int32(666), v.GetLibraryPort())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.InitializeRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("1.2.3.4", *v.GetLibraryAddress())
		s.Equal(int32(666), v.GetLibraryPort())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.InitializeRequest); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal("1.2.3.4", *v.GetLibraryAddress())
		s.Equal(int32(666), v.GetLibraryPort())
	}
}

func (s *UnitTestSuite) TestInitializeReply() {
	var message base.IProxyMessage = cluster.NewInitializeReply()
	proxyMessage := message.GetProxyMessage()
	serializedMessage, err := proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.InitializeReply); ok {
		s.Equal(int64(0), v.GetRequestID())
		s.Equal(messages.None, v.GetErrorType())
		s.Nil(v.GetError())
		s.Nil(v.GetErrorDetails())

		// Round-trip

		v.SetRequestID(int64(555))
		s.Equal(int64(555), v.GetRequestID())
		v.SetErrorType(messages.Custom)
		s.Equal(messages.Custom, v.GetErrorType())

		errStr := "MyError"
		v.SetError(&errStr)
		s.Equal("MyError", *v.GetError())

		errDetailsStr := "MyError Details"
		v.SetErrorDetails(&errDetailsStr)
		s.Equal("MyError Details", *v.GetErrorDetails())
	}

	proxyMessage = message.GetProxyMessage()
	serializedMessage, err = proxyMessage.Serialize(false)
	s.NoError(err)

	message, err = base.Deserialize(bytes.NewBuffer(serializedMessage), false)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.InitializeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
	}

	message, err = s.echoToConnection(message)
	s.NoError(err)
	s.NotNil(message)

	if v, ok := message.(*cluster.InitializeReply); ok {
		s.Equal(int64(555), v.GetRequestID())
		s.Equal(messages.Custom, v.GetErrorType())
		s.Equal("MyError", *v.GetError())
		s.Equal("MyError Details", *v.GetErrorDetails())
	}
}

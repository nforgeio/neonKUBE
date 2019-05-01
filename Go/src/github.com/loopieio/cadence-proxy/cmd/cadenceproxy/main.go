package main

import (
	"flag"
	"os"

	"go.uber.org/zap"
	"go.uber.org/zap/zapcore"

	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/endpoints"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/base"
	clustermessages "github.com/loopieio/cadence-proxy/cmd/cadenceproxy/messages/cluster"
	"github.com/loopieio/cadence-proxy/cmd/cadenceproxy/server"
)

func init() {
	base.InitProxyMessage()
	clustermessages.FillMessageTypeStructMap()
}

func main() {

	// variables to put command line args in
	var address, logLevel string
	var debugMode bool

	// define the flags and parse them
	flag.StringVar(&address, "listen", "127.0.0.2:5000", "Address for the Cadence Proxy Server to listen on")
	flag.StringVar(&logLevel, "log-level", "info", "The log level when running the proxy")
	flag.BoolVar(&debugMode, "debug", false, "Set to debug mode")
	flag.Parse()

	// set the log level and if the program should run in debug mode
	setLogLevelAndDebugMode(logLevel, debugMode)

	// create the instance, set the routes,
	// and start the server
	instance := server.NewInstance(address)
	endpoints.SetupRoutes(instance.Router)
	instance.Start()
}

func setLogLevelAndDebugMode(logLevel string, debugMode bool) {

	// new *zap.Logger
	// new zapcore.EncoderConfig for the logger
	var logger *zap.Logger
	var encoderCfg zapcore.EncoderConfig

	// new AtomicLevel for dynamic logging level
	atom := zap.NewAtomicLevel()

	switch debugMode {
	case true:

		// set the log level
		atom.SetLevel(zap.DebugLevel)

		// set Debug in endpoints
		endpoints.Debug = debugMode

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

	default:

		// set the log level
		switch logLevel {
		case "panic":
			atom.SetLevel(zap.PanicLevel)
		case "fatal":
			atom.SetLevel(zap.FatalLevel)
		case "error":
			atom.SetLevel(zap.ErrorLevel)
		case "warn":
			atom.SetLevel(zap.WarnLevel)
		case "debug":
			atom.SetLevel(zap.DebugLevel)
		default:
			atom.SetLevel(zap.InfoLevel)
		}

		// create the logger
		encoderCfg = zap.NewProductionEncoderConfig()
		encoderCfg.TimeKey = "Time"
		encoderCfg.MessageKey = "Message"
		encoderCfg.LevelKey = "Level"
		logger = zap.New(zapcore.NewCore(
			zapcore.NewJSONEncoder(encoderCfg),
			zapcore.Lock(os.Stdout),
			atom,
		))
		defer logger.Sync()
	}

	// set the global logger
	_ = zap.ReplaceGlobals(logger)
}

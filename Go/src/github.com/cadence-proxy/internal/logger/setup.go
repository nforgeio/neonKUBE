//-----------------------------------------------------------------------------
// FILE:		setup.go
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

package logger

import (
	"os"
	"time"

	"go.uber.org/zap"
	"go.uber.org/zap/zapcore"
)

// SetLogger takes a log level and bool and sets
// the global zap.Logger to a custom configured logger where the properties
// are specified in the parameters
//
// param logLevel string -> the log level to set in the global logger
//
// param debugMode bool -> run in debug mode or not
func SetLogger(logLevel string, debugMode bool) {

	// new *zap.Logger
	// new zapcore.EncoderConfig for the logger
	var logger *zap.Logger
	var encoderCfg zapcore.EncoderConfig

	// new AtomicLevel for dynamic logging level
	atom := zap.NewAtomicLevel()

	// switch on debug mode
	switch debugMode {
	case true:

		// set the log level
		atom.SetLevel(zap.DebugLevel)

		// configure the logger
		encoderCfg = zap.NewDevelopmentEncoderConfig()

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

		// configure the logger
		encoderCfg = zap.NewProductionEncoderConfig()
	}

	// encodings
	encoderCfg.EncodeTime = syslogTimeEncoder
	encoderCfg.EncodeCaller = zapcore.ShortCallerEncoder
	encoderCfg.EncodeLevel = zapcore.CapitalColorLevelEncoder

	// keys
	encoderCfg.CallerKey = "caller"
	encoderCfg.TimeKey = "ts"
	encoderCfg.LevelKey = "lvl"
	encoderCfg.MessageKey = "msg"

	// create the logger
	enc := zapcore.NewConsoleEncoder(encoderCfg)
	logger = zap.New(zapcore.NewCore(
		enc,
		zapcore.Lock(os.Stdout),
		atom,
	), zap.AddCaller())

	// set the global logger
	_ = zap.ReplaceGlobals(logger)
}

func syslogTimeEncoder(t time.Time, enc zapcore.PrimitiveArrayEncoder) {
	enc.AppendString(t.Format("Jan  2 15:04:05"))
}

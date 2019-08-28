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
// param l LogLevel -> the log level to set in the global logger
//
// param debugMode bool -> run in debug mode or not
//
// returns *zap.Logger -> the configured global zap logger.
// Can also be accessed via zap.L().
func SetLogger(l LogLevel, debugMode bool) *zap.Logger {

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
		// configure the logger
		atom.SetLevel(zap.DebugLevel)
		encoderCfg = zap.NewDevelopmentEncoderConfig()

	default:

		// set the log level
		switch l {
		case Panic:
			atom.SetLevel(zap.PanicLevel)
		case Error:
			atom.SetLevel(zap.ErrorLevel)
		case Warn:
			atom.SetLevel(zap.WarnLevel)
		case Debug:
			atom.SetLevel(zap.DebugLevel)
		case Info:
			atom.SetLevel(zap.InfoLevel)
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

	// create the logger
	enc := zapcore.NewConsoleEncoder(encoderCfg)
	logger = zap.New(zapcore.NewCore(
		enc,
		zapcore.Lock(os.Stdout),
		atom,
	), zap.AddCaller())

	// set the global logger
	_ = zap.ReplaceGlobals(logger)

	return logger
}

func syslogTimeEncoder(t time.Time, enc zapcore.PrimitiveArrayEncoder) {
	enc.AppendString(t.Format("Jan  2 15:04:05"))
}

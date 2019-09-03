//-----------------------------------------------------------------------------
// FILE:		logger_setup.go
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

package endpoints

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
// param zapcore.LevelEnabler -> a zapcore.LevelEnabler interface
//
// param debug bool -> run in debug mode or not
//
// param debugPrelaunched bool -> indicates that the logger should be configured for
// the proxy to run in debugPrelaunched mode.
//
// returns *zap.Logger -> the configured global zap logger.
// Can also be accessed via zap.L().
func SetLogger(enab zapcore.LevelEnabler, debug, debugPrelaunched bool) (logger *zap.Logger) {
	cfg := zapcore.EncoderConfig{
		TimeKey:      "time",
		MessageKey:   "msg",
		NameKey:      "name",
		LevelKey:     "lvl",
		CallerKey:    "caller",
		EncodeTime:   syslogTimeEncoder,
		EncodeCaller: zapcore.ShortCallerEncoder,
	}

	// set config
	var enc zapcore.Encoder
	if debug {
		cfg.EncodeLevel = zapcore.CapitalColorLevelEncoder
		enc = zapcore.NewConsoleEncoder(cfg)
	} else {
		cfg.EncodeLevel = zapcore.CapitalLevelEncoder
		enc = zapcore.NewJSONEncoder(cfg)
	}

	// create the core
	core := NewCore(
		enc,
		zapcore.Lock(os.Stdout),
		enab,
		debugPrelaunched,
	)

	// create the logger
	logger = zap.New(core, zap.AddCaller())
	defer logger.Sync()

	// make global logger
	_ = zap.ReplaceGlobals(logger)

	return
}

func syslogTimeEncoder(t time.Time, enc zapcore.PrimitiveArrayEncoder) {
	enc.AppendString(t.Format("Jan 2 15:04:05"))
}

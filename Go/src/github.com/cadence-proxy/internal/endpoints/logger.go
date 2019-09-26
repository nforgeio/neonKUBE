//-----------------------------------------------------------------------------
// FILE:		logger.go
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
	"context"
	"os"
	"time"

	"go.uber.org/zap"
	"go.uber.org/zap/zapcore"

	"github.com/cadence-proxy/internal"
	"github.com/cadence-proxy/internal/messages"
	dotnetlogger "github.com/cadence-proxy/internal/messages/dotnet-logger"
)

type (
	Core struct {
		zapcore.LevelEnabler
		enc zapcore.Encoder
		out zapcore.WriteSyncer
		d   bool
	}
)

func NewCore(
	enc zapcore.Encoder,
	ws zapcore.WriteSyncer,
	enab zapcore.LevelEnabler,
	debug bool) zapcore.Core {
	return &Core{
		LevelEnabler: enab,
		enc:          enc,
		out:          ws,
		d:            debug,
	}
}

func (c Core) With(fields []zapcore.Field) zapcore.Core {
	clone := c.clone()
	for i := range fields {
		fields[i].AddTo(clone.enc)
	}
	return clone
}

func (c Core) Check(entry zapcore.Entry, checkedEntry *zapcore.CheckedEntry) *zapcore.CheckedEntry {
	if c.Enabled(entry.Level) {
		return checkedEntry.AddCore(entry, c)
	}
	return checkedEntry
}

func (c Core) Write(entry zapcore.Entry, fields []zapcore.Field) error {
	ctx, cancel := context.WithTimeout(context.Background(), time.Second*5)
	defer cancel()

	if !c.d {
		return sendLogRequest(ctx, entry)
	}
	buf, err := c.enc.EncodeEntry(entry, fields)
	if err != nil {
		return err
	}
	_, err = c.out.Write(buf.Bytes())
	buf.Free()
	if err != nil {
		return err
	}
	if entry.Level > zapcore.ErrorLevel {
		c.Sync()
	}

	return sendLogRequest(ctx, entry)
}

func (c Core) Sync() error {
	return c.out.Sync()
}

func (c Core) clone() *Core {
	return &Core{
		LevelEnabler: c.LevelEnabler,
		enc:          c.enc,
		out:          c.out,
		d:            c.d,
	}
}

func sendLogRequest(ctx context.Context, entry zapcore.Entry) error {
	requestID := NextRequestID()
	logRequest := messages.NewLogRequest()
	logMessage := entry.Message
	logLevel, err := dotnetlogger.ZapLevelToLogLevel(entry.Level)
	if err != nil {
		return err
	}

	logRequest.SetRequestID(requestID)
	logRequest.SetClientID(LoggerClientID)
	logRequest.SetTimeUtc(entry.Time)
	logRequest.SetLogLevel(logLevel)
	logRequest.SetLogMessage(&logMessage)
	if entry.LoggerName == internal.CadenceLoggerName {
		logRequest.SetFromCadence(true)
	}

	op := messages.NewOperation(requestID, logRequest)
	op.SetChannel(make(chan interface{}))
	Operations.Add(requestID, op)

	go sendMessage(logRequest)

	result := <-op.GetChannel()
	switch s := result.(type) {
	case error:
		return s
	default:
		return nil
	}
}

// SetLogger takes a log level and bool and sets
// the global zap.Logger to a custom configured logger where the properties
// are specified in the parameters
//
// param zapcore.LevelEnabler -> a zapcore.LevelEnabler interface
//
// param debug bool -> indicates whether the cadence-proxy is running as its
// own process in debug mode.
//
// returns *zap.Logger -> the configured zap logger.
func SetLogger(enab zapcore.LevelEnabler, debug bool) (logger *zap.Logger) {
	core := NewCore(
		NewEncoder(),
		zapcore.Lock(os.Stdout),
		enab,
		debug,
	)

	// create the logger
	logger = zap.New(core)
	defer logger.Sync()

	return
}

// NewEncoder creates a new zapcore.Encoder interface with custom
// formatting.
func NewEncoder() (enc zapcore.Encoder) {
	cfg := zapcore.EncoderConfig{
		TimeKey:        "time",
		MessageKey:     "msg",
		NameKey:        "name",
		LevelKey:       "lvl",
		CallerKey:      "caller",
		LineEnding:     "\n",
		EncodeName:     zapcore.FullNameEncoder,
		EncodeCaller:   zapcore.ShortCallerEncoder,
		EncodeTime:     syslogTimeEncoder,
		EncodeDuration: zapcore.StringDurationEncoder,
		EncodeLevel:    zapcore.CapitalLevelEncoder,
	}
	enc = zapcore.NewConsoleEncoder(cfg)

	return
}

func syslogTimeEncoder(t time.Time, enc zapcore.PrimitiveArrayEncoder) {
	enc.AppendString(t.Format("Jan 2 15:04:05"))
}

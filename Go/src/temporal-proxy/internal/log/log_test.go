//-----------------------------------------------------------------------------
// FILE:		log_test.go
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

package log_test

import (
	"testing"

	"temporal-proxy/internal/log"

	"go.uber.org/goleak"
	"go.uber.org/zap/zapcore"

	"github.com/stretchr/testify/suite"
)

var (
	// log levels
	none, debug, sinfo, info, warn, errorlvl, serror, critical log.Level
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
	s.setupTestSuiteServer()

	// run the tests
	suite.Run(t, s)

	// check for goroutine leaks
	goleak.VerifyNone(t)
}

func (s *UnitTestSuite) setupTestSuiteServer() {
	none = log.None
	debug = log.Debug
	sinfo = log.SInfo
	info = log.Info
	warn = log.Warn
	errorlvl = log.Error
	serror = log.SError
	critical = log.Critical
}

func (s *UnitTestSuite) Test_Enabled() {
	// none
	s.False(none.Enabled(zapcore.DebugLevel))
	s.False(none.Enabled(zapcore.InfoLevel))
	s.False(none.Enabled(zapcore.WarnLevel))
	s.False(none.Enabled(zapcore.ErrorLevel))
	s.False(none.Enabled(zapcore.DPanicLevel))
	s.False(none.Enabled(zapcore.PanicLevel))
	s.False(none.Enabled(zapcore.FatalLevel))

	// debug
	s.True(debug.Enabled(zapcore.DebugLevel))
	s.True(debug.Enabled(zapcore.InfoLevel))
	s.True(debug.Enabled(zapcore.WarnLevel))
	s.True(debug.Enabled(zapcore.ErrorLevel))
	s.True(debug.Enabled(zapcore.DPanicLevel))
	s.True(debug.Enabled(zapcore.PanicLevel))
	s.True(debug.Enabled(zapcore.FatalLevel))

	// sinfo
	s.False(sinfo.Enabled(zapcore.DebugLevel))
	s.True(sinfo.Enabled(zapcore.InfoLevel))
	s.True(sinfo.Enabled(zapcore.WarnLevel))
	s.True(sinfo.Enabled(zapcore.ErrorLevel))
	s.True(sinfo.Enabled(zapcore.DPanicLevel))
	s.True(sinfo.Enabled(zapcore.PanicLevel))
	s.True(sinfo.Enabled(zapcore.FatalLevel))

	// info
	s.False(info.Enabled(zapcore.DebugLevel))
	s.True(info.Enabled(zapcore.InfoLevel))
	s.True(info.Enabled(zapcore.WarnLevel))
	s.True(info.Enabled(zapcore.ErrorLevel))
	s.True(info.Enabled(zapcore.DPanicLevel))
	s.True(info.Enabled(zapcore.PanicLevel))
	s.True(info.Enabled(zapcore.FatalLevel))

	// warn
	s.False(warn.Enabled(zapcore.DebugLevel))
	s.False(warn.Enabled(zapcore.InfoLevel))
	s.True(warn.Enabled(zapcore.WarnLevel))
	s.True(warn.Enabled(zapcore.ErrorLevel))
	s.True(warn.Enabled(zapcore.DPanicLevel))
	s.True(warn.Enabled(zapcore.PanicLevel))
	s.True(warn.Enabled(zapcore.FatalLevel))

	// error
	s.False(errorlvl.Enabled(zapcore.DebugLevel))
	s.False(errorlvl.Enabled(zapcore.InfoLevel))
	s.False(errorlvl.Enabled(zapcore.WarnLevel))
	s.True(errorlvl.Enabled(zapcore.ErrorLevel))
	s.True(errorlvl.Enabled(zapcore.DPanicLevel))
	s.True(errorlvl.Enabled(zapcore.PanicLevel))
	s.True(errorlvl.Enabled(zapcore.FatalLevel))

	// serror
	s.False(serror.Enabled(zapcore.DebugLevel))
	s.False(serror.Enabled(zapcore.InfoLevel))
	s.False(serror.Enabled(zapcore.WarnLevel))
	s.False(serror.Enabled(zapcore.ErrorLevel))
	s.True(serror.Enabled(zapcore.DPanicLevel))
	s.True(serror.Enabled(zapcore.PanicLevel))
	s.True(serror.Enabled(zapcore.FatalLevel))

	// critical
	s.False(critical.Enabled(zapcore.DebugLevel))
	s.False(critical.Enabled(zapcore.InfoLevel))
	s.False(critical.Enabled(zapcore.WarnLevel))
	s.False(critical.Enabled(zapcore.ErrorLevel))
	s.True(critical.Enabled(zapcore.DPanicLevel))
	s.True(critical.Enabled(zapcore.PanicLevel))
	s.True(critical.Enabled(zapcore.FatalLevel))
}

func (s *UnitTestSuite) Test_String() {
	s.Equal("None", none.String())
	s.Equal("Debug", debug.String())
	s.Equal("SInfo", sinfo.String())
	s.Equal("Info", info.String())
	s.Equal("Warn", warn.String())
	s.Equal("Error", errorlvl.String())
	s.Equal("SError", serror.String())
	s.Equal("Critical", critical.String())
}

func (s *UnitTestSuite) Test_ParseLogLeve() {
	s.Equal(log.None, log.ParseLevel(none.String()))
	s.Equal(log.Debug, log.ParseLevel(debug.String()))
	s.Equal(log.SInfo, log.ParseLevel(sinfo.String()))
	s.Equal(log.Info, log.ParseLevel(info.String()))
	s.Equal(log.Warn, log.ParseLevel(warn.String()))
	s.Equal(log.SError, log.ParseLevel(serror.String()))
	s.Equal(log.Error, log.ParseLevel(errorlvl.String()))
	s.Equal(log.Critical, log.ParseLevel(critical.String()))
	s.Equal(log.None, log.ParseLevel("-1"))
}

func (s *UnitTestSuite) Test_LevelToZapLevel() {
	a, err := log.LevelToZapLevel(none)
	s.Error(err)
	s.Equal(zapcore.InfoLevel, a)

	a, err = log.LevelToZapLevel(debug)
	s.NoError(err)
	s.Equal(zapcore.DebugLevel, a)

	a, err = log.LevelToZapLevel(sinfo)
	s.NoError(err)
	s.Equal(zapcore.InfoLevel, a)

	a, err = log.LevelToZapLevel(info)
	s.NoError(err)
	s.Equal(zapcore.InfoLevel, a)

	a, err = log.LevelToZapLevel(info)
	s.NoError(err)
	s.Equal(zapcore.InfoLevel, a)

	a, err = log.LevelToZapLevel(warn)
	s.NoError(err)
	s.Equal(zapcore.WarnLevel, a)

	a, err = log.LevelToZapLevel(errorlvl)
	s.NoError(err)
	s.Equal(zapcore.ErrorLevel, a)

	a, err = log.LevelToZapLevel(serror)
	s.NoError(err)
	s.Equal(zapcore.ErrorLevel, a)

	a, err = log.LevelToZapLevel(critical)
	s.NoError(err)
	s.Equal(zapcore.PanicLevel, a)
}

func (s *UnitTestSuite) Test_ZapLevelToLevel() {
	a, err := log.ZapLevelToLevel(zapcore.DebugLevel)
	s.NoError(err)
	s.Equal(log.Debug, a)

	a, err = log.ZapLevelToLevel(zapcore.InfoLevel)
	s.NoError(err)
	s.Equal(log.Info, a)

	a, err = log.ZapLevelToLevel(zapcore.WarnLevel)
	s.NoError(err)
	s.Equal(log.Warn, a)

	a, err = log.ZapLevelToLevel(zapcore.ErrorLevel)
	s.NoError(err)
	s.Equal(log.Error, a)

	a, err = log.ZapLevelToLevel(zapcore.PanicLevel)
	s.NoError(err)
	s.Equal(log.Critical, a)
}

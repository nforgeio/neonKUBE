//-----------------------------------------------------------------------------
// FILE:		log_level.go
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

package log

import (
	"fmt"
	"strings"

	"go.uber.org/zap/zapcore"
)

// Level enumerates the possible log levels.  Note that the relative
// ordinal values of  these definitions are used when deciding
// to log an event when a specific Level is
// set.  Only events with log levels less than or equal to the
// current level will be logged.
type Level int32

const (
	// None - Logging is disabled.
	None Level = 0

	// Debug - Describes detailed debug or diagnostic information.
	Debug Level = 1

	// SInfo - Describes a non-error security operation or condition, such as a
	// a successful login or authentication.
	SInfo Level = 2

	// Info -Describes a normal operation or condition.
	Info Level = 3

	// Warn - An unusual condition has been detected that may ultimately lead to an error.
	Warn Level = 4

	// Error - An error has been detected.
	Error Level = 5

	// SError - A security related error has occurred.  Errors indicate a problem that may be
	// transient, be recovered from, or are perhaps more serious.
	SError Level = 6

	// Critical - A critical or fatal error has been detected.  These errors indicate that
	// a very serious failure has occurred that may have crashed the program or
	// at least seriousoly impacts its functioning.
	Critical Level = 7
)

// Enabled checks a Level against a zapcore.Level
// to see if logging should be enabled for that level.
// With this method Level implements zapcore.LevelEnabler
// interface.
//
// param zapLvl zapcore.Level -> the zapcore.Level to check against.
//
// return bool -> true if logging should be enabled at the
// zapcore.Level input, false if it should not be.
func (l Level) Enabled(zapLvl zapcore.Level) bool {
	if l == None {
		return false
	}

	lvl, err := ZapLevelToLevel(zapLvl)
	if err != nil {
		panic(err)
	}

	return lvl >= l
}

// String translates a LogLeve enum into
// the corresponding string
func (l Level) String() string {
	switch l {
	case None:
		return "None"
	case Debug:
		return "Debug"
	case SInfo:
		return "SInfo"
	case Info:
		return "Info"
	case Warn:
		return "Warn"
	case Error:
		return "Error"
	case SError:
		return "SError"
	case Critical:
		return "Critical"
	default:
		return "None"
	}
}

// ParseLevel takes a string value and returns
// the corresponding Level
func ParseLevel(value string) Level {
	value = strings.ToUpper(value)
	switch value {
	case "NONE":
		return None
	case "DEBUG":
		return Debug
	case "SINFO":
		return SInfo
	case "INFO":
		return Info
	case "WARN":
		return Warn
	case "SERROR":
		return SError
	case "ERROR":
		return Error
	case "CRITICAL":
		return Critical
	default:
		return None
	}
}

// LevelToZapLevel takes a LogLeve and attempts
// to map it to a zapcore.Level.  If it can, then it returns
// the corresponding zapcore.Level, if not zapcore.InfoLevel is returned
// along with an error.
//
// param value Level -> Level to map to zapcore.Level.
//
// returns zapcore.Level -> zapcore.Level that maps to the input Level,
// or zapcore.InfoLevel upon failure to map.
//
// returns error -> error upon failure to map to a zapcore.Level.
func LevelToZapLevel(value Level) (zapcore.Level, error) {
	switch value {
	case Debug:
		return zapcore.DebugLevel, nil
	case SInfo:
		return zapcore.InfoLevel, nil
	case Info:
		return zapcore.InfoLevel, nil
	case Warn:
		return zapcore.WarnLevel, nil
	case Error:
		return zapcore.ErrorLevel, nil
	case SError:
		return zapcore.ErrorLevel, nil
	case Critical:
		return zapcore.PanicLevel, nil
	default:
		return zapcore.InfoLevel, fmt.Errorf("input Level %s does not map to existing zapcore.level", value.String())
	}
}

// ZapLevelToLevel attempts to map a zapcore.Level to a
// Level.
//
// param value zapcore.Level -> zapcore.Level to attempt to map
// to Level.
//
// returns Level -> Level corresponding to input
// zapcore.Level.
//
// returns error -> error if the input zapcore.Level cannot be
// mapped to a Level.
func ZapLevelToLevel(value zapcore.Level) (Level, error) {
	switch value {
	case zapcore.DebugLevel:
		return Debug, nil
	case zapcore.InfoLevel:
		return Info, nil
	case zapcore.WarnLevel:
		return Warn, nil
	case zapcore.ErrorLevel:
		return Error, nil
	case zapcore.DPanicLevel:
		return Critical, nil
	case zapcore.PanicLevel:
		return Critical, nil
	case zapcore.FatalLevel:
		return Critical, nil
	default:
		return Info, fmt.Errorf("input zapcore.Level %s does not map to existing Level", value.String())
	}
}

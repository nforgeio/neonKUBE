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

package logger

// LogLevel enumerates the possible log levels.  Note that the relative
// ordinal values of  these definitions are used when deciding
// to log an event when a specific LogLevel is
// set.  Only events with log levels less than or equal to the
// current level will be logged.
type LogLevel int32

const (
	// Logging is disabled.
	None LogLevel = 0

	// A critical or fatal error has been detected.
	Panic LogLevel = 100

	// A security related error has occurred.
	SError LogLevel = 200

	// An error has been detected.
	Error LogLevel = 300

	// An unusual condition has been detected that may ultimately lead to an error.
	Warn LogLevel = 400

	// Describes a normal operation or condition.
	Info LogLevel = 500

	// Describes a non-error security operation or condition, such as a
	// a login or authentication.
	SInfo LogLevel = 600

	// Describes detailed debug or diagnostic information.
	Debug LogLevel = 700
)

// String translates a LogLeve enum into
// the corresponding string
func (l LogLevel) String() string {
	switch l {
	case None:
		return "None"
	case Panic:
		return "Critical"
	case SError:
		return "SError"
	case Error:
		return "Error"
	case Warn:
		return "Warn"
	case Info:
		return "Info"
	case SInfo:
		return "SInfo"
	case Debug:
		return "Debug"
	default:
		return "None"
	}
}

// ParseLogLevel takes a string value and returns
// the corresponding LogLevel
func ParseLogLevel(value string) LogLevel {
	switch value {
	case "None":
		return None
	case "Panic":
		return Panic
	case "Critical":
		return Panic
	case "SError":
		return SError
	case "Error":
		return Error
	case "Warn":
		return Warn
	case "Info":
		return Info
	case "SInfo":
		return SInfo
	case "Debug":
		return Debug
	default:
		return None
	}
}

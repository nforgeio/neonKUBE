package base

import (
	"bytes"
	"reflect"
	"testing"
	"time"
)

func TestDeserialize(t *testing.T) {
	type args struct {
		b *bytes.Buffer
	}
	tests := []struct {
		name string
		args args
		want ProxyMessage
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := Deserialize(tt.args.b); !reflect.DeepEqual(got, tt.want) {
				t.Errorf("Deserialize() = %v, want %v", got, tt.want)
			}
		})
	}
}

func TestProxyMessage_Serialize(t *testing.T) {
	tests := []struct {
		name string
		pm   *ProxyMessage
		want []byte
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := tt.pm.Serialize(); !reflect.DeepEqual(got, tt.want) {
				t.Errorf("ProxyMessage.Serialize() = %v, want %v", got, tt.want)
			}
		})
	}
}

func Test_writeInt32(t *testing.T) {
	type args struct {
		value int32
	}
	tests := []struct {
		name  string
		args  args
		wantW string
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			w := &bytes.Buffer{}
			writeInt32(w, tt.args.value)
			if gotW := w.String(); gotW != tt.wantW {
				t.Errorf("writeInt32() = %v, want %v", gotW, tt.wantW)
			}
		})
	}
}

func Test_writeString(t *testing.T) {
	type args struct {
		b     *bytes.Buffer
		value *string
	}
	tests := []struct {
		name string
		args args
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			writeString(tt.args.b, tt.args.value)
		})
	}
}

func Test_readString(t *testing.T) {
	type args struct {
		b *bytes.Buffer
	}
	tests := []struct {
		name string
		args args
		want *string
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := readString(tt.args.b); !reflect.DeepEqual(got, tt.want) {
				t.Errorf("readString() = %v, want %v", got, tt.want)
			}
		})
	}
}

func Test_readInt32(t *testing.T) {
	type args struct {
		b *bytes.Buffer
	}
	tests := []struct {
		name string
		args args
		want int32
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := readInt32(tt.args.b); got != tt.want {
				t.Errorf("readInt32() = %v, want %v", got, tt.want)
			}
		})
	}
}

func TestProxyMessage_ProxyMessageToString(t *testing.T) {
	tests := []struct {
		name string
		pm   *ProxyMessage
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			tt.pm.String()
		})
	}
}

func TestProxyMessage_GetStringProperty(t *testing.T) {
	type args struct {
		key string
	}
	tests := []struct {
		name string
		pm   *ProxyMessage
		args args
		want *string
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := tt.pm.GetStringProperty(tt.args.key); !reflect.DeepEqual(got, tt.want) {
				t.Errorf("ProxyMessage.GetStringProperty() = %v, want %v", got, tt.want)
			}
		})
	}
}

func TestProxyMessage_GetIntProperty(t *testing.T) {
	type args struct {
		key string
	}
	tests := []struct {
		name string
		pm   *ProxyMessage
		args args
		want int32
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := tt.pm.GetIntProperty(tt.args.key); got != tt.want {
				t.Errorf("ProxyMessage.GetIntProperty() = %v, want %v", got, tt.want)
			}
		})
	}
}

func TestProxyMessage_GetLongProperty(t *testing.T) {
	type args struct {
		key string
	}
	tests := []struct {
		name string
		pm   *ProxyMessage
		args args
		want int64
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := tt.pm.GetLongProperty(tt.args.key); got != tt.want {
				t.Errorf("ProxyMessage.GetLongProperty() = %v, want %v", got, tt.want)
			}
		})
	}
}

func TestProxyMessage_GetBoolProperty(t *testing.T) {
	type args struct {
		key string
	}
	tests := []struct {
		name string
		pm   *ProxyMessage
		args args
		want bool
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := tt.pm.GetBoolProperty(tt.args.key); got != tt.want {
				t.Errorf("ProxyMessage.GetBoolProperty() = %v, want %v", got, tt.want)
			}
		})
	}
}

func TestProxyMessage_GetDoubleProperty(t *testing.T) {
	type args struct {
		key string
	}
	tests := []struct {
		name string
		pm   *ProxyMessage
		args args
		want float64
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := tt.pm.GetDoubleProperty(tt.args.key); got != tt.want {
				t.Errorf("ProxyMessage.GetDoubleProperty() = %v, want %v", got, tt.want)
			}
		})
	}
}

func TestProxyMessage_GetDateTimeProperty(t *testing.T) {
	type args struct {
		key string
	}
	tests := []struct {
		name string
		pm   *ProxyMessage
		args args
		want time.Time
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := tt.pm.GetDateTimeProperty(tt.args.key); !reflect.DeepEqual(got, tt.want) {
				t.Errorf("ProxyMessage.GetDateTimeProperty() = %v, want %v", got, tt.want)
			}
		})
	}
}

func TestProxyMessage_GetTimeSpanProperty(t *testing.T) {
	type args struct {
		key string
	}
	tests := []struct {
		name string
		pm   *ProxyMessage
		args args
		want time.Duration
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			if got := tt.pm.GetTimeSpanProperty(tt.args.key); !reflect.DeepEqual(got, tt.want) {
				t.Errorf("ProxyMessage.GetTimeSpanProperty() = %v, want %v", got, tt.want)
			}
		})
	}
}

func TestProxyMessage_SetStringProperty(t *testing.T) {
	type args struct {
		key   string
		value string
	}
	tests := []struct {
		name string
		pm   *ProxyMessage
		args args
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			tt.pm.SetStringProperty(tt.args.key, tt.args.value)
		})
	}
}

func TestProxyMessage_SetIntProperty(t *testing.T) {
	type args struct {
		key   string
		value int32
	}
	tests := []struct {
		name string
		pm   *ProxyMessage
		args args
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			tt.pm.SetIntProperty(tt.args.key, tt.args.value)
		})
	}
}

func TestProxyMessage_SetLongProperty(t *testing.T) {
	type args struct {
		key   string
		value int64
	}
	tests := []struct {
		name string
		pm   *ProxyMessage
		args args
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			tt.pm.SetLongProperty(tt.args.key, tt.args.value)
		})
	}
}

func TestProxyMessage_SetBoolProperty(t *testing.T) {
	type args struct {
		key   string
		value bool
	}
	tests := []struct {
		name string
		pm   *ProxyMessage
		args args
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			tt.pm.SetBoolProperty(tt.args.key, tt.args.value)
		})
	}
}

func TestProxyMessage_SetDoubleProperty(t *testing.T) {
	type args struct {
		key   string
		value float64
	}
	tests := []struct {
		name string
		pm   *ProxyMessage
		args args
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			tt.pm.SetDoubleProperty(tt.args.key, tt.args.value)
		})
	}
}

func TestProxyMessage_SetDateTimeProperty(t *testing.T) {
	type args struct {
		key   string
		value time.Time
	}
	tests := []struct {
		name string
		pm   *ProxyMessage
		args args
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			tt.pm.SetDateTimeProperty(tt.args.key, tt.args.value)
		})
	}
}

func TestProxyMessage_SetTimeSpanProperty(t *testing.T) {
	type args struct {
		key   string
		value time.Duration
	}
	tests := []struct {
		name string
		pm   *ProxyMessage
		args args
	}{
		// TODO: Add test cases.
	}
	for _, tt := range tests {
		t.Run(tt.name, func(t *testing.T) {
			tt.pm.SetTimeSpanProperty(tt.args.key, tt.args.value)
		})
	}
}

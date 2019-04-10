.PHONY: test bins clean
default: test

# replace \ with /
NF_ROOT := $(subst \,/,$(NF_ROOT))

export GOPATH := $(NF_ROOT)/Go
export PATH := $(GOPATH)/bin:$(PATH)
export BIN := $(NF_ROOT)/Build

PROJECT_ROOT = github.com/loopieio/go-cadence-proxy
PROGS = cadenceproxy
BUILD := ./build
APP_DIR=./cmd/cadenceproxy
TEST_DIRS=./cmd/cadenceproxy/test
ALL_SRC := $(shell find ./cmd/cadenceproxy -name "*.go")

dep-ensured:
	dep ensure

cadenceproxywindows: dep-ensured $(ALL_SRC)
	go build -i -o $(BIN)/gocadenceproxy-windows $(APP_DIR)/*.go

cadenceproxylinux: dep-ensured $(ALL_SRC)
	env GOOS=linux GOARCH=amd64 go build -ldflags="-w -s" -o $(BIN)/gocadenceproxy-linux $(APP_DIR)/*.go

cadenceproxy: cadenceproxylinux \
	cadenceproxywindows

bins: cadenceproxy \

test: bins
	@rm -f test/gocadenceproxy.test
	@rm -f test/logs/test.log
	@echo $(TEST_DIRS)
	@for dir in $(TEST_DIRS); do \
		go test -v -o test/gocadenceproxy.test "$$dir" | tee -a test/logs/test.log; \
	done;

clean:
	rm -f $(BIN)/gocadenceproxy-windows
	rm -f $(BIN)/gocadenceproxy-linux
	rm -Rf $(BUILD)
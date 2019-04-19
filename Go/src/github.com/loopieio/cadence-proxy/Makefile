.PHONY: test bins clean
default: test

export PATH := $(GOPATH)/bin:$(PATH)

PROJECT_ROOT = github.com/loopieio/cadence-proxy
PROGS = cadenceproxy
BUILD := ./build
APP_DIR=./cmd/cadenceproxy
TEST_DIRS=./cmd/cadenceproxy/messages/base
ALL_SRC := $(shell find ./cmd/cadenceproxy -name "*.go")

dep-ensured:
	dep ensure

cadenceproxywindows: dep-ensured $(ALL_SRC)
	go build -i -ldflags="-w -s" -v -o bin/gocadenceproxy.exe $(APP_DIR)/*.go

cadenceproxylinux: dep-ensured $(ALL_SRC)
	env GOOS=linux GOARCH=amd64 go build -i -ldflags="-w -s" -v -o bin/gocadenceproxy $(APP_DIR)/*.go

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
	rm -f test/gocadenceproxy.test
	rm -f test/logs/test.log
	rm -f bin/gocadenceproxy.exe
	rm -f bin/gocadenceproxy
	rm -Rf $(BUILD)
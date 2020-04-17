module neonkube.io/test-wf-args

go 1.13

require (
	github.com/pborman/uuid v1.2.0
	github.com/uber-go/tally v3.3.15+incompatible
	go.uber.org/cadence v0.11.2
	go.uber.org/yarpc v1.44.0
	go.uber.org/zap v1.14.1
	gopkg.in/yaml.v2 v2.2.8
)

replace github.com/apache/thrift => github.com/apache/thrift v0.0.0-20190309152529-a9b748bb0e02

replace github.com/opentracing/opentracing-go => github.com/opentracing/opentracing-go v1.1.0

replace github.com/uber-go/mapdecode => github.com/uber-go/mapdecode v1.0.0

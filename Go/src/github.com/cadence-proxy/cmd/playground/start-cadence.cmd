docker run --detach -p 7933-7939:7933-7939 -p 8088:8088 nkubeio/cadence-test:latest
sleep 60
docker run --network=host --rm ubercadence/cli:master --do test-domain domain register -rd 1

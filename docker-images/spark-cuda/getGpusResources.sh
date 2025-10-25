#!/usr/bin/env bash

ADDRS=$(ls /dev/nvidia[0-9]* | sed 's:/dev/nvidia::g' | tr '\n' ',' | sed 's/,$//')
echo "{\"name\": \"gpu\", \"addresses\":[\"$ADDRS\"]}"

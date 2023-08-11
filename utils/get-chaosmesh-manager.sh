#!/bin/bash

eval "$(pulumi stack output --shell)"

echo "The token for the Manager role"
echo "-------"
echo ""

kubectl create token $chaosMeshManagerRole --namespace otel-demo
#!/bin/bash

eval "$(pulumi stack output --shell)"

echo "The token for the Viewer role"
echo "-------"
echo ""

kubectl create token $chaosMeshViewerRole --namespace otel-demo
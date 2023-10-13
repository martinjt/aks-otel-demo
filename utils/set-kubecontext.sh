#!/bin/bash

eval "$(pulumi stack output --shell)"

echo "Setting kubectl context to $clusterName in $clusterResourceGroup"
az aks get-credentials -n $clusterName -g $clusterResourceGroup --overwrite-existing --context aks-otel-demo
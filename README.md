# An example of deploying the OpenTelemetry Demo to Azure AKS


Set the default location for pulumi to deploy to.
```bash
pulumi config set azure-native:location eastus
```

# Deploying

```bash
az login
```

Follow the prompts to get your credentials.

Add the Honeycomb API Key

```shell
pulumi config set --secret honeycombKey <key>
```

Add the DNS zone config

```shell
pulumi config set --secret dns-zone-id <resourceId for the DNS zone>
```

## Troubleshooting

### Deploy a shell

There is a busybox shell that can be deployed so you can do things inside the cluster. This will give you a pod that you can shell into.

```shell
kubectl apply -f utils/shell.yaml
```

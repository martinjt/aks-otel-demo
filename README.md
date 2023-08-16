# An example of deploying the OpenTelemetry Demo to Azure AKS

## Logging into the pulumi state

I advise using azure blob, and therefore you login using this:

```shell
cd infra
az login
pulumi login azblob://<container>?storage_account=<storageaccount>
```

Then select the stack you're working on

```shell
pulumi stack select <stack name>
```

## Setting up pulumi settings

Set the default location for pulumi to deploy to.

```bash
pulumi config set azure-native:location eastus
```

Add the Honeycomb API Key

```shell
pulumi config set --secret honeycombKey <key>
```

Add the DNS zone config

```shell
pulumi config set --secret dns-zone-id <resourceId for the DNS zone>
```

## Accessing the k8s cluster

You can get the credentials for the cluster into your kubeconfig using the utils script

You'll need to be logged into pulumi, with a passphrase set in your environment variable.

```shell
cd infra
bash ../utils/set-kubecontext.sh
```

## Access Chaos Mesh

You can get the token for accessing chaos mesh using the scripts in utils

Viewer:

```shell
cd infra
bash ../utils/get-chaosmesh-viewer.sh
```

Manager:

```shell
cd infra
bash ../utils/get-chaosmesh-manager.sh
```

## Troubleshooting

### Deploy a shell

There is a busybox shell that can be deployed so you can do things inside the cluster. This will give you a pod that you can shell into.

```shell
kubectl apply -f utils/shell.yaml
```

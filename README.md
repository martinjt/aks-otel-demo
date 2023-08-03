# An example of deploying the OpenTelemetry Demo to Azure AKS

# Deploying

Login to azure, this is needed as that's where the current state of the Pulumi deployment is stored. If you're using this to deploy your own cluster, you can skip this to store it locally etc.

```bash
az login
```
(follow the login prompts to Azure)

Login to the Pulumi state store

```bash
AZURE_STORAGE_ACCOUNT=<the-storage-account-name> pulumi login azblob://<the-container-name>
```

You'll need to make sure you have the `Azure Blob Data Contributor` role for that storage account or you won't be able to login.


# Settings

Honeycomb API Key
```shell
pulumi config set --secret honeycombKey <key>
```

The DNS Zone Id (required to add domain names that map to the ingress.)
```shell
pulumi config set --secret dns-zone-id <resourceId for the DNS zone>
```

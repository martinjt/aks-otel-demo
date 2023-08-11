using Pulumi;
using Pulumi.Kubernetes.Helm.V3;
using Pulumi.Kubernetes.Types.Inputs.Helm.V3;
using System.Collections.Generic;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Core.V1;
using System.IO;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace infra.Applications;

public class Refinery : ComponentResource
{
    public Refinery(string name, RefineryArgs args, ComponentResourceOptions? options = null)
        : base(name, "aks-otel-demo:chart:refinery", options)
    {

        var config = new Config();
        var apiKey = config.RequireSecret("honeycombKey");

        var refineryNamespace = new Namespace("refinery", new NamespaceArgs {
            Metadata = new ObjectMetaArgs {
                Name = "refinery"
            }
        }, new CustomResourceOptions { Provider = options?.Provider!});

        var secretApiKey = new Secret("honeycomb-api-key-refinery", new SecretArgs
        {
            Metadata = new ObjectMetaArgs {
                Namespace = refineryNamespace.Metadata.Apply(m => m.Name)
            },
            StringData = {
                ["honeycomb-api-key"] = apiKey
            }
        }, new CustomResourceOptions { Provider = options?.Provider!});

        var refineryRulesConfigMap = new ConfigMap("refinery-rules", new ConfigMapArgs {
            Metadata = new ObjectMetaArgs {
                Name = "refinery-rules",
                Namespace = refineryNamespace.Metadata.Apply(m => m.Name)
            },
            Data = {
                ["rules.yaml"] = File.ReadAllText("./config-files/refinery/rules.yaml")
        }}, new CustomResourceOptions { Provider = options?.Provider!});

        var refinery = new Release("refinery", new ReleaseArgs {
            Chart = "refinery",
            Name = "refinery",
            Namespace = refineryNamespace.Metadata.Apply(m => m.Name),
            RepositoryOpts = new RepositoryOptsArgs {
                Repo = "https://honeycombio.github.io/helm-charts"
            },
            ValueYamlFiles = new FileAsset("./config-files/refinery/values.yaml"),
            Values = new Dictionary<string, object> {
                ["environment"] = new [] {
                    new Dictionary<string, object> {
                        ["name"] = "REFINERY_HONEYCOMB_API_KEY",
                        ["valueFrom"] = new Dictionary<string, object> {
                            ["secretKeyRef"] = new Dictionary<string, object> {
                                ["name"] = secretApiKey.Id.Apply(a => a.Split("/")[1]),
                                ["key"] = "honeycomb-api-key"
                            }
                        }
                    }
                },
                ["RulesConfigMapName"] = refineryRulesConfigMap.Metadata.Apply(m => m.Name)
            }
        }, new CustomResourceOptions { Provider = options?.Provider!});

        RefineryServiceName = Output.Format($"{refinery.Namespace}.{refinery.Name}");
    }

    public Output<string> RefineryServiceName { get; set; }
}

public class RefineryArgs : ResourceArgs
{
}
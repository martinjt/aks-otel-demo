using Pulumi;
using Pulumi.Kubernetes.Helm.V3;
using Pulumi.Kubernetes.Types.Inputs.Helm.V3;
using System.Collections.Generic;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace infra.Applications;

public class OtelCollector : ComponentResource
{
    public OtelCollector(string name, OtelCollectorArgs args, ComponentResourceOptions? options = null)
        : base(name, "aks-otel-demo:chart:otel-demo", options)
    {
        var config = new Config();
        var apiKey = config.RequireSecret("honeycombKey");

        var otelColNamespace = new Namespace("otel-col", new NamespaceArgs {
            Metadata = new ObjectMetaArgs {
                Name = "otel-collector"
            }
        });
        
        var secretApiKey = new Secret("honeycomb-api-key-otel-collector", new SecretArgs
        {
            Metadata = new ObjectMetaArgs {
                Namespace = otelColNamespace.Metadata.Apply(m => m.Name)
            },
            StringData = {
                ["honeycomb-api-key"] = apiKey
            }
        });


        var values =new Dictionary<string, object> {
            ["extraEnvs"] = new [] {
                new Dictionary<string, object> {
                    ["name"] = "HONEYCOMB_API_KEY",
                    ["valueFrom"] = new Dictionary<string, object> {
                        ["secretKeyRef"] = new Dictionary<string, object> {
                            ["name"] = secretApiKey.Id.Apply(a => a.Split("/")[1]),
                            ["key"] = "honeycomb-api-key"
                        }
                    }
                },
                new Dictionary<string, object> {
                    ["name"] = "REFINERY_ADDRESS",
                    ["value"] = args.RefineryName
                },
            },
        };

        var otelCollectorRelease = new Release("otel-collector", new ReleaseArgs {
            Chart = "opentelemetry-collector",
            Name = "otel-collector",
            Version = "0.59.2",
            Namespace = otelColNamespace.Metadata.Apply(m => m.Name),
            RepositoryOpts = new RepositoryOptsArgs {
                Repo = "https://open-telemetry.github.io/opentelemetry-helm-charts"
            },
            DependencyUpdate = true,
            ValueYamlFiles = new FileAsset("./config-files/collector/values.yaml"),
            Values = values
        }, new CustomResourceOptions
        {
            IgnoreChanges = { "resourceNames" }
        });

        this.CollectorName = Output.Format($"{otelCollectorRelease.Namespace}.{otelCollectorRelease.Name}");
    }


    public Output<string> CollectorName { get; set; } = null!;
}

public class OtelCollectorArgs
{
    public Input<string> RefineryName { get; set; } = null!;
}
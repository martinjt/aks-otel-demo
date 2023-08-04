using Pulumi;
using Pulumi.Kubernetes.Helm.V3;
using Pulumi.Kubernetes.Networking.V1;
using Pulumi.Kubernetes.Types.Inputs.Networking.V1;
using Pulumi.Kubernetes.Types.Inputs.Helm.V3;
using System.Collections.Generic;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Core.V1;
using System.IO;

public class OtelDemo : ComponentResource
{
    public OtelDemo(string name, OtelDemoArgs args, ComponentResourceOptions? options = null)
        : base(name, "aks-otel-demo:chart:otel-demo", options)
    {

        var config = new Config();
        var apiKey = config.RequireSecret("honeycombKey");

        var secretApiKey = new Secret("honeycomb-api-key", new SecretArgs
        {
            StringData = {
                ["honeycomb-api-key"] = apiKey
            }
        }, new CustomResourceOptions
        {
            Provider = args.Provider
        });

        var refineryRulesConfigMap = new ConfigMap("refinery-rules", new ConfigMapArgs {
            Data = {
                ["rules.yaml"] = File.ReadAllText("./config-files/refinery/rules.yaml")
        }}, new CustomResourceOptions
        {
            Provider = args.Provider
        });

        var refinery = new Release("refinery", new ReleaseArgs {
            Chart = "refinery",
            Name = "sampling-proxy",
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
        }, new CustomResourceOptions
        {
            Provider = args.Provider
        });

        var release = new Release("otel-demo", new ReleaseArgs {
            Chart = "opentelemetry-demo",
            Name = "otel-demo",
            RepositoryOpts = new RepositoryOptsArgs {
                Repo = "https://open-telemetry.github.io/opentelemetry-helm-charts"
            },
            ValueYamlFiles = new FileAsset("./config-files/collector/values.yaml"),
            Values = new Dictionary<string, object> {
                ["opentelemetry-collector"] = new Dictionary<string, object> {
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
                    },
                    ["config"] = new Dictionary<string, object> {
                        ["exporter"] =  new Dictionary<string, object> {
                            ["otlp/honeycomb"] = new Dictionary<string, object> {
                                ["endpoint"] = refinery.Name.Apply(n => $"http://{n}-refinery:4317"),
                                ["headers"] = new Dictionary<string, object> {
                                    ["x-honeycomb-team"] = apiKey
                                },
                                ["tls"] = new Dictionary<string, object> {
                                    ["insecure"] = true
                                }
                            }
                        }
                    }
                }
            }
        }, new CustomResourceOptions
        {
            Provider = args.Provider
        });

        var ingress = new Ingress("otel-demo-frontend", new IngressArgs {
            Spec = new IngressSpecArgs {
                IngressClassName = "webapprouting.kubernetes.azure.com",
                Rules = new IngressRuleArgs {
                    Host = "www.demo.onlyspans.com",
                    Http = new HTTPIngressRuleValueArgs {
                        Paths = new [] {
                            new HTTPIngressPathArgs {
                                Path = "/",
                                PathType = "Prefix",
                                Backend = new IngressBackendArgs {
                                    Service = new IngressServiceBackendArgs {
                                        Name = "otel-demo-frontendproxy",
                                        Port = new ServiceBackendPortArgs {
                                            Number = 8080
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }, new CustomResourceOptions {
            DependsOn = new [] { release },
            Provider = args.Provider
        });
    }
}

public class OtelDemoArgs
{
    public ProviderResource Provider { get; set; } = null!;
}
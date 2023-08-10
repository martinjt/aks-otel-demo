using Pulumi;
using Pulumi.Kubernetes.Helm.V3;
using Pulumi.Kubernetes.Networking.V1;
using Pulumi.Kubernetes.Types.Inputs.Networking.V1;
using Pulumi.Kubernetes.Types.Inputs.Helm.V3;
using System.Collections.Generic;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;

namespace infra.Applications;

public class OtelDemo : ComponentResource
{
    public OtelDemo(string name, OtelDemoArgs args, ComponentResourceOptions? options = null)
        : base(name, "aks-otel-demo:chart:otel-demo", options)
    {
        var config = new Config();
        var apiKey = config.RequireSecret("honeycombKey");

        var otelDemoNamespace = new Namespace("otel-demo", new NamespaceArgs {
            Metadata = new ObjectMetaArgs {
                Name = "otel-demo"
            }
        });
        
        var secretApiKey = new Secret("honeycomb-api-key-otel-demo", new SecretArgs
        {
            Metadata = new ObjectMetaArgs {
                Namespace = otelDemoNamespace.Metadata.Apply(m => m.Name)
            },
            StringData = {
                ["honeycomb-api-key"] = apiKey
            }
        });

        var servicesToSetAffinityOn = new HashSet<string> {
            "accountingService",
            "shippingService"
        };

        var values =new Dictionary<string, object> {
                ["default"] = new Dictionary<string, object> {
                    ["replicas"] = 2
                },
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
                        ["exporters"] =  new Dictionary<string, object> {
                            ["otlp/honeycomb"] = new Dictionary<string, object> {
                                ["endpoint"] = Output.Format($"http://{args.RefineryName}:4317"),
                                ["headers"] = new Dictionary<string, object> {
                                    ["x-honeycomb-team"] = apiKey
                                },
                                ["tls"] = new Dictionary<string, object> {
                                    ["insecure"] = true
                                }
                            }
                        }
                    }
                },
                ["components"] = new Dictionary<string, object>()
            };
        foreach (var serviceName in servicesToSetAffinityOn)
            ((Dictionary<string, object>)values["components"])
                .Add(serviceName,new Dictionary<string, object> {
                    ["schedulingRules"] = GenerateSchedulingRules("otel-demo", serviceName)
                });

        var otelDemoRelease = new Release("otel-demo", new ReleaseArgs {
            Chart = "../opentelemetry-helm-charts/charts/opentelemetry-demo",
            Name = "otel-demo",
            Namespace = otelDemoNamespace.Metadata.Apply(m => m.Name),
            DependencyUpdate = true,
            ValueYamlFiles = new FileAsset("./config-files/collector/values.yaml"),
            Values = values
        }, new CustomResourceOptions
        {
            IgnoreChanges = { "resourceNames" }
        });

        var ingress = new Ingress("otel-demo-frontend", new IngressArgs {
            Metadata = new ObjectMetaArgs {
                Namespace = otelDemoNamespace.Metadata.Apply(m => m.Name),
            },
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
            DependsOn = new [] { otelDemoRelease },
        });

        this.Namespace = otelDemoNamespace.Metadata.Apply(m => m.Name);
    }

    private Dictionary<string, object> GenerateSchedulingRules(string releaseName, string name) => 
        new () {
            ["affinity"] = new Dictionary<string, object> {
                ["podAntiAffinity"] = new Dictionary<string, object> {
                    ["preferredDuringSchedulingIgnoredDuringExecution"] = new [] {
                        new Dictionary<string, object> {
                            ["podAffinityTerm"] = new Dictionary<string, object> {
                                ["labelSelector"] = new Dictionary<string, object> {
                                    ["matchExpressions"] = new [] {
                                        new Dictionary<string, object> {
                                            ["key"] = "app.kubernetes.io/component",
                                            ["operator"] = "In",
                                            ["values"] = new [] {
                                                name
                                            }
                                        }
                                    }
                                },
                                ["topologyKey"] = "kubernetes.io/hostname"

                            },
                            ["weight"] = 100
                        }
                    }
                }
            }
        };

// default:
//   schedulingRules:
//     affinity:
//       nodeAffinity:
//         requiredDuringSchedulingIgnoredDuringExecution:
//           nodeSelectorTerms:
//             - matchExpressions:
//                 - key: name
//                   operator: In
//                   values:
//                     - '{{ include "otel-demo.name" . }}-{{ .name }}'
//                   topologyKey: kubernetes.io/hostname

    public Output<string> Namespace { get; set; } = null!;
}

public class OtelDemoArgs
{
    public Input<string> RefineryName { get; set; } = null!;
}
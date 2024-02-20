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

        var otelDemoNamespace = new Namespace("otel-demo", new NamespaceArgs {
            Metadata = new ObjectMetaArgs {
                Name = "otel-demo"
            }
        }, new CustomResourceOptions { Provider = options?.Provider!});
        
        var servicesToSetAffinityOn = new HashSet<string> {
            "accountingService",
            "shippingService"
        };

        var values =new Dictionary<string, object> {
                ["default"] = new Dictionary<string, object> {
                    ["replicas"] = 2,
                    ["envOverrides"] = new [] {
                        new Dictionary<string, object> {
                            ["name"] = "OTEL_COLLECTOR_NAME",
                            ["valueFrom"] = new Dictionary<string, object> {
                                ["fieldRef"] = new Dictionary<string, object> {
                                    ["fieldPath"] = "status.hostIP"
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

        values.Add(
            "opentelemetry-collector", new Dictionary<string, object> {
                ["enabled"] = false
            }
        );

        var otelDemoRelease = new Release("otel-demo", new ReleaseArgs {
            Chart = "opentelemetry-demo",
            Name = "otel-demo",
            RepositoryOpts = new RepositoryOptsArgs {
                Repo = "https://open-telemetry.github.io/opentelemetry-helm-charts"
            },
            Version = "0.28.3",
            Namespace = otelDemoNamespace.Metadata.Apply(m => m.Name),
            DependencyUpdate = true,
            Values = values
        }, new CustomResourceOptions
        {
            IgnoreChanges = { "resourceNames" },
            Provider = options?.Provider!
        });

        var ingress = new Ingress("otel-demo-frontend", new IngressArgs {
            Metadata = new ObjectMetaArgs {
                Namespace = otelDemoNamespace.Metadata.Apply(m => m.Name),
            },
            Spec = new IngressSpecArgs {
                IngressClassName = "webapprouting.kubernetes.azure.com",
                Rules = new IngressRuleArgs {
                    Host = Output.Format($"www.{args.DomainName}"),
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
            Provider = options?.Provider!
        });

        Namespace = otelDemoNamespace.Metadata.Apply(m => m.Name);
    }

    private static Dictionary<string, object> GenerateSchedulingRules(string releaseName, string name) => 
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

    public Output<string> Namespace { get; set; } = null!;
}

public class OtelDemoArgs
{
    public Input<string> CollectorName { get; set; } = null!;
    public Input<string> DomainName { get; set; } = null!;
}
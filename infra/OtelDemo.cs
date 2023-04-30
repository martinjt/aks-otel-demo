using Pulumi;
using Pulumi.Kubernetes.Helm.V3;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using Pulumi.Kubernetes.Networking.V1;
using Pulumi.Kubernetes.Types.Inputs.Networking.V1;
using Pulumi.Kubernetes.Types.Inputs.Helm.V3;

public class OtelDemo : ComponentResource
{
    public OtelDemo(string name, OtelDemoArgs args, ComponentResourceOptions? options = null)
        : base(name, "aks-otel-demo:chart:otel-demo", options)
    {
        var release = new Release("otel-demo", new ReleaseArgs {
            Chart = "opentelemetry-demo",
            Name = "otel-demo",
            RepositoryOpts = new RepositoryOptsArgs {
                Repo = "https://open-telemetry.github.io/opentelemetry-helm-charts"
            }
        }, new CustomResourceOptions
        {
            Provider = args.Provider
        });

        var ingress = new Ingress("otel-demo-frontend", new IngressArgs {
            Metadata = new ObjectMetaArgs {
                Annotations = {
                    ["kubernetes.io/ingress.class"] = "azure/application-gateway",
                    ["appgw.ingress.kubernetes.io/use-private-ip"] = "false"
                }
            },
            Spec = new IngressSpecArgs {
                Rules = new IngressRuleArgs {
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
            DependsOn = new [] { release }
        });
    }
}

public class OtelDemoArgs
{
    public ProviderResource Provider { get; set; } = null!;
}
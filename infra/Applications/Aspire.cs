using Pulumi;
using Pulumi.Kubernetes.Types.Inputs.Apps.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using Pulumi.Kubernetes.Core.V1;

using Deployment = Pulumi.Kubernetes.Apps.V1.Deployment;
using Service = Pulumi.Kubernetes.Core.V1.Service;

public class Aspire : ComponentResource
{
    const string Image = "mcr.microsoft.com/dotnet/nightly/aspire-dashboard:8.0.0-preview.4";
    static InputMap<string> Labels = new InputMap<string> { { "app", "aspire" } };

    public Output<string> AspireServiceName { get; }

    public Aspire(string name, AspireArgs args, ComponentResourceOptions? options = null)
        : base(name, "aks-otel-demo:apps:aspire", options)
    {
        var aspireNamespace = new Namespace("aspire", new NamespaceArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "aspire"
            }
        }, new CustomResourceOptions { Provider = options?.Provider! });

        var deployment = new Deployment("aspire", new DeploymentArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Namespace = aspireNamespace.Metadata.Apply(m => m.Name),
            },
            Spec = new DeploymentSpecArgs
            {
                Selector = new LabelSelectorArgs { MatchLabels = Labels },
                Replicas = 1,
                Template = new PodTemplateSpecArgs
                {
                    Metadata = new ObjectMetaArgs { Labels = Labels },
                    Spec = new PodSpecArgs
                    {
                        Containers = new ContainerArgs[]
                        {
                            new() {
                                Name = "aspire",
                                Image = Image,
                                Ports = new ContainerPortArgs[]
                                {
                                    new() {
                                        Name = "otlp-grpc",
                                        ContainerPortValue = 18889,
                                    },
                                    new() {
                                        Name = "dashboard",
                                        ContainerPortValue = 18888
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }, new CustomResourceOptions { Provider = options?.Provider! });

        var service = new Service("aspire", new ServiceArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Namespace = aspireNamespace.Metadata.Apply(m => m.Name),
                Name = "aspire-dashboard"
            },
            Spec = new ServiceSpecArgs
            {
                Selector = deployment.Spec.Apply(s => s.Selector.MatchLabels),
                Ports = new ServicePortArgs[]
                {
                    new() {
                        Name = "otlp-grpc",
                        Port = 4317,
                        TargetPort = 18889
                    },
                    new() {
                        Name = "dashboard",
                        Port = 18888,
                        TargetPort = 18888
                    }
                }
            }
        }, new CustomResourceOptions { Provider = options?.Provider! });
        AspireServiceName = Output.Format($"{aspireNamespace.Metadata.Apply(m => m.Name)}.{service.Metadata.Apply(m => m.Name)}");
    }
}

public class AspireArgs
{
}
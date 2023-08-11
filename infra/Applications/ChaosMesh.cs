using Pulumi;
using Pulumi.Kubernetes.Types.Inputs.Meta.V1;
using Pulumi.Kubernetes.Core.V1;
using Pulumi.Kubernetes.Types.Inputs.Core.V1;
using Pulumi.Kubernetes.Helm.V3;
using Pulumi.Kubernetes.Types.Inputs.Helm.V3;
using Pulumi.Kubernetes.Rbac.V1;
using Pulumi.Kubernetes.Types.Inputs.Rbac.V1;

namespace infra.Applications;

public class ChaosMesh : ComponentResource
{
    public ChaosMesh(string name, ChaosMeshArgs args, ComponentResourceOptions? options = null)
        : base(name, "aks-otel-demo:chart:chaos-mesh", options)
    {
        var chaosDemoNamespace = new Namespace("chaos-mesh", new NamespaceArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Name = "chaos-mesh"
            }
        }, new CustomResourceOptions { Provider = options?.Provider!});
        var otelDemoRelease = new Release("chaos-mesh", new ReleaseArgs
        {
            Chart = "chaos-mesh",
            Name = "chaos-mesh",
            Namespace = chaosDemoNamespace.Metadata.Apply(m => m.Name),
            RepositoryOpts = new RepositoryOptsArgs
            {
                Repo = "https://charts.chaos-mesh.org"
            },
            ValueYamlFiles = new FileAsset("./config-files/chaos-mesh/values.yaml"),
        }, new CustomResourceOptions { Provider = options?.Provider!});

        ViewerRole = CreateViewRole(args, options);
        ManagerRole = CreateManagerRole(args, options);
    }

    private static Output<string> CreateViewRole(ChaosMeshArgs args, ComponentResourceOptions? options)
    {
        var viewerServiceAccount = new ServiceAccount("viewer", new ServiceAccountArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Namespace = args.OtelDemoNamespace
            }
        }, new CustomResourceOptions { Provider = options?.Provider!});

        var viewerRole = new Role("viewer", new RoleArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Namespace = args.OtelDemoNamespace
            },
            Rules = {
                new PolicyRuleArgs {
                    ApiGroups = { "" },
                    Resources = { "pods", "namespaces" },
                    Verbs = { "get", "list", "watch" },
                },
                new PolicyRuleArgs {
                    ApiGroups = { "chaos-mesh.org" },
                    Resources = { "*" },
                    Verbs = { "get", "list", "watch" },
                }
            }
        }, new CustomResourceOptions { Provider = options?.Provider!});

        var viewerRoleBinding = new RoleBinding("viewer", new RoleBindingArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Namespace = args.OtelDemoNamespace
            },
            RoleRef = new RoleRefArgs
            {
                ApiGroup = "rbac.authorization.k8s.io",
                Kind = viewerRole.Kind,
                Name = viewerRole.Metadata.Apply(m => m.Name)
            },
            Subjects = {
                new SubjectArgs {
                    Kind = viewerServiceAccount.Kind,
                    Name = viewerServiceAccount.Metadata.Apply(m => m.Name),
                    Namespace = viewerServiceAccount.Metadata.Apply(m => m.Namespace)
                }
            }
        }, new CustomResourceOptions { Provider = options?.Provider!});

        return viewerServiceAccount.Metadata.Apply(m => m.Name);
    }
    private static Output<string> CreateManagerRole(ChaosMeshArgs args, ComponentResourceOptions? options)
    {
        var managerServiceAccount = new ServiceAccount("manager", new ServiceAccountArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Namespace = args.OtelDemoNamespace
            }
        }, new CustomResourceOptions { Provider = options?.Provider!});

        var managerRole = new Role("manager", new RoleArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Namespace = args.OtelDemoNamespace
            },
            Rules = {
                new PolicyRuleArgs {
                    ApiGroups = { "" },
                    Resources = { "pods", "namespaces" },
                    Verbs = { "get", "list", "watch" },
                },
                new PolicyRuleArgs {
                    ApiGroups = { "chaos-mesh.org" },
                    Resources = { "*" },
                    Verbs = { "get", "list", "watch", "create", "delete", "patch", "update" },
                }
            }
        }, new CustomResourceOptions { Provider = options?.Provider!});

        var managerRoleBinding = new RoleBinding("manager", new RoleBindingArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Namespace = args.OtelDemoNamespace
            },
            RoleRef = new RoleRefArgs
            {
                ApiGroup = "rbac.authorization.k8s.io",
                Kind = managerRole.Kind,
                Name = managerRole.Metadata.Apply(m => m.Name)
            },
            Subjects = {
                new SubjectArgs {
                    Kind = managerServiceAccount.Kind,
                    Name = managerServiceAccount.Metadata.Apply(m => m.Name),
                    Namespace = managerServiceAccount.Metadata.Apply(m => m.Namespace)
                }
            }
        }, new CustomResourceOptions { Provider = options?.Provider!});
        return managerServiceAccount.Metadata.Apply(m => m.Name);
    }

    [Output("ChaosMesh-ViewerRole")]
    public Output<string> ViewerRole { get; set; } = null!;

    [Output("ChaosMesh-ManagerRole")]
    public Output<string> ManagerRole { get; set; } = null!;
}


public class ChaosMeshArgs : ResourceArgs
{
    public Input<string> OtelDemoNamespace { get; set; } = null!;
}
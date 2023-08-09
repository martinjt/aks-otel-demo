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
        });
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
        });

        CreateViewRole(args);
    }

    private static void CreateViewRole(ChaosMeshArgs args)
    {
        var viewerServiceAccount = new ServiceAccount("viewer", new ServiceAccountArgs
        {
            Metadata = new ObjectMetaArgs
            {
                Namespace = args.OtelDemoNamespace
            }
        });

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
        });

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
        });
    }
}

public class ChaosMeshArgs : ResourceArgs
{
    public Input<string> OtelDemoNamespace { get; set; }
}
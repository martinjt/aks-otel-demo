using System;
using System.Text;
using Pulumi;
using Pulumi.AzureAD;
using Pulumi.AzureNative.ContainerService;
using Pulumi.AzureNative.ContainerService.Inputs;
using Pulumi.AzureNative.Resources;
using Pulumi.Tls;
using K8s = Pulumi.Kubernetes;

public class AKSCluster : ComponentResource
{
    public AKSCluster(string name, AKSClusterArgs? args, ComponentResourceOptions? options = null)
        : base("aks-otel-demo:aks:cluster", name, args, options)
    {
        // Create an Azure Resource Group
        var resourceGroup = new ResourceGroup(name);

        // Create an AD service principal
        var adApp = new Application(name, new ApplicationArgs
        {
            DisplayName = name
        });
        var adSp = new ServicePrincipal("aksSp", new ServicePrincipalArgs
        {
            ApplicationId = adApp.ApplicationId
        });
        var adSpPassword = new ServicePrincipalPassword("aksSpPassword", new ServicePrincipalPasswordArgs
        {
            ServicePrincipalId = adSp.Id,
            EndDate = "2099-01-01T00:00:00Z"
        });

        // Generate an SSH key
        var sshKey = new PrivateKey("ssh-key", new PrivateKeyArgs
        {
            Algorithm = "RSA",
            RsaBits = 4096
        });

        var cluster = new ManagedCluster(name, new ManagedClusterArgs
        {
            ResourceGroupName = resourceGroup.Name,
            AddonProfiles = {
                ["IngressApplicationGateway"] = new ManagedClusterAddonProfileArgs {
                    Enabled = true,
                    Config = {
                        ["subnetCIDR"] = "10.225.0.0/16"
                    }
                }
            },
            AgentPoolProfiles =
            {
                new ManagedClusterAgentPoolProfileArgs
                {
                    Count = 3,
                    MaxPods = 110,
                    Mode = AgentPoolMode.System,
                    Name = "agentpool",
                    OsType = OSType.Linux,
                    Type = AgentPoolType.VirtualMachineScaleSets,
                    VmSize = "Standard_DS2_v2",
                }
            },
            DnsPrefix = "AzureNativeprovider",
            EnableRBAC = true,
            KubernetesVersion = "1.26.3",
            LinuxProfile = new ContainerServiceLinuxProfileArgs
            {
                AdminUsername = "testuser",
                Ssh = new ContainerServiceSshConfigurationArgs
                {
                    PublicKeys =
                    {
                        new ContainerServiceSshPublicKeyArgs
                        {
                            KeyData = sshKey.PublicKeyOpenssh,
                        }
                    }
                }
            },
            NodeResourceGroup = $"{name}-nodes",
            ServicePrincipalProfile = new ManagedClusterServicePrincipalProfileArgs
            {
                ClientId = adApp.ApplicationId,
                Secret = adSpPassword.Value
            }
        });

        // Export the KubeConfig
        this.KubeConfig = ListManagedClusterUserCredentials.Invoke(
            new ListManagedClusterUserCredentialsInvokeArgs
            {
                ResourceGroupName = resourceGroup.Name,
                ResourceName = cluster.Name
            })
            .Apply(x => x.Kubeconfigs[0].Value)
            .Apply(Convert.FromBase64String)
            .Apply(Encoding.UTF8.GetString);

        this.Provider = new K8s.Provider("k8s-provider", new K8s.ProviderArgs
        {
            KubeConfig = KubeConfig
        });

        this.ClusterName = cluster.Name;
        this.ClusterResourceGroup = resourceGroup.Name;
    }

    [Output("clusterName")]
    public Output<string> ClusterName { get; set; }

    [Output("clusterResourceGroup")]
    public Output<string> ClusterResourceGroup { get; set; }

    [Output("kubeconfig")]
    public Output<string> KubeConfig { get; set; }

    public K8s.Provider Provider { get; set; }

}

public class AKSClusterArgs : Pulumi.ResourceArgs
{
}

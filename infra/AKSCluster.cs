using System;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Azure.Core;
using Pulumi;
using Pulumi.AzureNative.Authorization;
using Pulumi.AzureNative.Compute;
using Pulumi.AzureNative.ContainerService.V20230502Preview;
using Pulumi.AzureNative.ContainerService.V20230502Preview.Inputs;
using Pulumi.AzureNative.ContainerService.V20230502Preview.Outputs;
using Pulumi.AzureNative.Network;
using Pulumi.AzureNative.Resources;
using Pulumi.Tls;
using K8s = Pulumi.Kubernetes;
using ResourceIdentityType = Pulumi.AzureNative.ContainerService.V20230502Preview.ResourceIdentityType;

public class AKSCluster : ComponentResource
{
    private const string DnsZoneContributorRoleDefinitionId = "/providers/Microsoft.Authorization/roleDefinitions/befefa01-2a29-4197-83a8-272ff33ce314";

    public AKSCluster(string name, AKSClusterArgs args, ComponentResourceOptions? options = null)
        : base("aks-otel-demo:aks:cluster", name, args, options)
    {
        
        var config = new Pulumi.Config();
        var location = config.Get("location") ?? "uksouth";

        // Create an Azure Resource Group
        var resourceGroup = new ResourceGroup(name, new ResourceGroupArgs
        {
            Location = location
        });
        // Generate an SSH key
        var sshKey = new PrivateKey("ssh-key", new PrivateKeyArgs
        {
            Algorithm = "RSA",
            RsaBits = 4096
        });

        var addonProfiles = new InputMap<ManagedClusterAddonProfileArgs>();
        if (args.CreateApplicationGateway)
            addonProfiles.Add("IngressApplicationGateway", new ManagedClusterAddonProfileArgs {
                Enabled = true,
                Config = {
                    ["subnetCIDR"] = "10.225.0.0/16"
                }
            });

        var cluster = new ManagedCluster(name, new ManagedClusterArgs
        {
            ResourceGroupName = resourceGroup.Name,
            NodeResourceGroup = resourceGroup.Name.Apply(rg => $"{rg}-nodes"),
            AddonProfiles = addonProfiles,
            Location = resourceGroup.Location,
            Identity = new ManagedClusterIdentityArgs
            {
                Type = ResourceIdentityType.SystemAssigned
            },
            DnsPrefix = "otel-demo",
            EnableRBAC = true,
            KubernetesVersion = "1.29.0",
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
            IngressProfile = new ManagedClusterIngressProfileArgs
            {
                WebAppRouting = new ManagedClusterIngressProfileWebAppRoutingArgs
                {
                    Enabled = true,
                    DnsZoneResourceId = args.DnsZoneId
                }
            },
            AgentPoolProfiles = new[]
            {
                new ManagedClusterAgentPoolProfileArgs
                {
                    Name = "basepool",
                    Count = 1,
                    MaxPods = 110,
                    Mode = AgentPoolMode.System,
                    OsType = OSType.Linux,
                    Type = AgentPoolType.VirtualMachineScaleSets,
                    VmSize = VirtualMachineSizeTypes.Standard_A2_v2.ToString(),
                }
            }
        });

        var agentPool = new AgentPool("agents", new AgentPoolArgs {
            ResourceGroupName = resourceGroup.Name,
            Count = 3,
            MaxPods = 110,
            Mode = AgentPoolMode.User,
            OsType = OSType.Linux,
            Type = AgentPoolType.VirtualMachineScaleSets,
            VmSize = VirtualMachineSizeTypes.Standard_DS2_v2.ToString(),
            ResourceName = cluster.Name,
        }, new CustomResourceOptions { 
            DeletedWith = cluster,
            ReplaceOnChanges = { "vmSize" },
            DeleteBeforeReplace = true });

        var roleAssignment = new RoleAssignment("cluster-dns-contributor", new()
        {
            PrincipalId = cluster.IngressProfile.Apply(ip => ip?.WebAppRouting!.Identity.ObjectId!),
            PrincipalType = PrincipalType.ServicePrincipal,
            RoleDefinitionId = DnsZoneContributorRoleDefinitionId,
            Scope = args.DnsZoneId
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
            KubeConfig = KubeConfig,
            EnableServerSideApply = true
        });

        this.ClusterName = cluster.Name;
        this.ClusterResourceGroup = resourceGroup.Name;
        if (args.CreateApplicationGateway)
            this.GatewayIp = cluster.AddonProfiles.GetApplicationGatewayIp();
    }

    [Output("clusterName")]
    public Output<string> ClusterName { get; set; }

    [Output("clusterResourceGroup")]
    public Output<string> ClusterResourceGroup { get; set; }

    [Output("kubeconfig")]
    public Output<string> KubeConfig { get; set; }

    [Output("GatewayIp")]
    public Output<string?> GatewayIp { get; set; } = null!;

    public K8s.Provider Provider { get; set; }

}

public class AKSClusterArgs : Pulumi.ResourceArgs
{
    public bool CreateApplicationGateway { get; set; } = false;
    public Input<string> DnsZoneId { get; set; } = null!;
}

internal static class ExtensionForCluster
{
    public static Output<string?> GetApplicationGatewayIp(this Output<ImmutableDictionary<string, ManagedClusterAddonProfileResponse>?> addonProfiles)
    {
        return addonProfiles.Apply(a =>
        {
            var appGatewayResourceId = new Azure.Core.ResourceIdentifier(
                a!["IngressApplicationGateway"]!
                    .Config!["effectiveApplicationGatewayId"]);

            var appGatewayDetails = GetApplicationGateway.Invoke(new GetApplicationGatewayInvokeArgs
            {
                ApplicationGatewayName = appGatewayResourceId.Name,
                ResourceGroupName = appGatewayResourceId.ResourceGroupName!
            });
            return appGatewayDetails.Apply<string?>(a =>
            {
                var publicIpId = a?.FrontendIPConfigurations.First()?
                    .PublicIPAddress?.Id;
                if (publicIpId == null)
                    return "";

                var publicIpResourceId = new ResourceIdentifier(publicIpId);
                var publicIp = GetPublicIPAddress.Invoke(new GetPublicIPAddressInvokeArgs
                {
                    PublicIpAddressName = publicIpResourceId.Name,
                    ResourceGroupName = publicIpResourceId.ResourceGroupName!
                });
                return publicIp.Apply(a => a.IpAddress);
            });
        });
    }

}

using System.Collections.Generic;
using Pulumi;
using infra.Applications;
using Pulumi.AzureNative.Network;

bool CreateChaosMesh = false;

return await Deployment.RunAsync(() =>
{
    var config = new Config();
    var dnsResourceGroup = config.Require("dnsResourceGroup");
    var dnsZoneName = config.Require("dnsZoneName");

    var dnsZone = GetZone.Invoke(new GetZoneInvokeArgs{
        ResourceGroupName = dnsResourceGroup,
        ZoneName = dnsZoneName
    });

    var cluster = new AKSCluster("aks-otel-demo", new AKSClusterArgs{
        DnsZoneId = dnsZone.Apply(d => d.Id)
    });
    
    var outputs = new Dictionary<string, object?>
    {
        ["clusterName"] = cluster.ClusterName,
        ["clusterResourceGroup"] = cluster.ClusterResourceGroup,
    };

    var refinery = new Refinery("refinery", new RefineryArgs(), 
        new ComponentResourceOptions { Provider = cluster.Provider });

    var otelCollector = new OtelCollector("otel-collector", new OtelCollectorArgs{
        RefineryName = refinery.RefineryServiceName,
    }, new ComponentResourceOptions { Provider = cluster.Provider });

    var otelDemo = new OtelDemo("otel-demo", new OtelDemoArgs {
        CollectorHostName = otelCollector.CollectorName,
        DomainName = dnsZoneName
    }, new ComponentResourceOptions { Provider = cluster.Provider });

    if (CreateChaosMesh)
    {
        var chaosMesh = new ChaosMesh("chaos-mesh", new ChaosMeshArgs {
            OtelDemoNamespace = otelDemo.Namespace,
            DomainName = dnsZoneName
        }, new ComponentResourceOptions { Provider = cluster.Provider });
        outputs.Add("chaosMeshViewerRole", chaosMesh.ViewerRole);
        outputs.Add("chaosMeshManagerRole", chaosMesh.ManagerRole);
    }

    return outputs;
});
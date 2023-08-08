using System.Collections.Generic;
using Pulumi;
using infra.Applications;

return await Pulumi.Deployment.RunAsync(() =>
{
    var cluster = new AKSCluster("aks-otel-demo", new AKSClusterArgs());
    
    var refinery = new Refinery("refinery", new RefineryArgs(), 
        new ComponentResourceOptions { Provider = cluster.Provider });

    var otelDemo = new OtelDemo("otel-demo", new OtelDemoArgs{
        RefineryName = refinery.RefineryServiceName
    }, new ComponentResourceOptions { Provider = cluster.Provider });

    return new Dictionary<string, object?>
    {
        ["clusterName"] = cluster.ClusterName,
        ["clusterResourceGroup"] = cluster.ClusterResourceGroup
    };
});
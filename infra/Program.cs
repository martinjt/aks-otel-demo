using System.Collections.Generic;
using Pulumi;
using infra.Applications;

return await Deployment.RunAsync(() =>
{
    var cluster = new AKSCluster("aks-otel-demo", new AKSClusterArgs());
    
    var refinery = new Refinery("refinery", new RefineryArgs(), 
        new ComponentResourceOptions { Provider = cluster.Provider });

    var otelCollector = new OtelCollector("otel-collector", new OtelCollectorArgs{
        RefineryName = refinery.RefineryServiceName
    }, new ComponentResourceOptions { Provider = cluster.Provider });

    var otelDemo = new OtelDemo("otel-demo", new OtelDemoArgs {
        CollectorName = otelCollector.CollectorName
    }, new ComponentResourceOptions { Provider = cluster.Provider });

    var chaosMesh = new ChaosMesh("chaos-mesh", new ChaosMeshArgs {
        OtelDemoNamespace = otelDemo.Namespace
    }, new ComponentResourceOptions { Provider = cluster.Provider });

    return new Dictionary<string, object?>
    {
        ["clusterName"] = cluster.ClusterName,
        ["clusterResourceGroup"] = cluster.ClusterResourceGroup
    };
});
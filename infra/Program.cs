using System.Collections.Generic;

return await Pulumi.Deployment.RunAsync(() =>
{
    var cluster = new AKSCluster("aks-otel-demo", new AKSClusterArgs());
    var otelDemo = new OtelDemo("otel-demo", new OtelDemoArgs{
        Provider = cluster.Provider
    });
    // Export the primary key of the Storage Account
    return new Dictionary<string, object?>
    {
        ["clusterName"] = cluster.ClusterName,
        ["clusterResourceGroup"] = cluster.ClusterResourceGroup,
        ["ingressControllerIp"] = cluster.GatewayIp
    };
});
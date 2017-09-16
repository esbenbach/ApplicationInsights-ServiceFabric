[context-screenshot-for-containers]: ./media/readme/context-for-containers.png
[context-screenshot-for-native-services]: ./media/readme/context-for-native-services.png

# Microsoft Application Insights for Service Fabric
This repository hosts code for functionality that augments Application Insights telemetry experience for Service Fabric. 

## Problem Statement
[Application Insights](http://azure.microsoft.com/services/application-insights/) is a service that lets you monitor your live application's performance and usage. ApplicationInsight SDKs is a family of nuget packages that lets you collect and send telemetry from your applications. Information about SDKs for different platforms and languages is available at the [Application Insights SDK page](https://github.com/Microsoft/ApplicationInsights-home). 

When the above mentioned SDKs collect application telemetry, they do not assume and record an application specific context because the context is environment specific. For microservices running inside service fabric, it is important to be able to recognize what service context the telemetry is generated in. For example request or dependency telemetry would not make sense without context information like service name, service type, instance / replica ids etc. 

## Solving the context problem
This repo provides code for a telemetry initializer (and some associated utility classes), shipped as nuget packages, specifically designed for Service Fabric. The initializer when used as described in the following sections, automatically augments telemetry collected by the different application insights SDKs that might be added to a service. For general information on Application Insights Telemetry Initializers follow this [blog entry](http://www.apmtips.com/blog/2014/12/01/telemetry-initializers/).

## Nuget Packages
This repository produces the following two nuget packages:
* [**Microsoft.ApplicationInsights.ServiceFabric.Native**](https://www.nuget.org/packages/Microsoft.ApplicationInsights.ServiceFabric.Native/1.1.0-beta1) - For use with Service Fabric's [native reliable services](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-reliable-services-introduction).
* [**Microsoft.ApplicationInsights.ServiceFabric**](https://www.nuget.org/packages/Microsoft.ApplicationInsights.ServiceFabric/1.1.0-beta1) - For use with [Guest Executable](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-deploy-existing-app) and [Guest container](https://docs.microsoft.com/en-us/azure/service-fabric/service-fabric-containers-overview) services (lift and shift scenarios).

## Scope
* This repository and the associated nuget packages, for now, only deals with `.Net Framework` and `.Net Core` applications.
* The nuget packages does **not** auto-collect or generate any telemetry. It merely adds-to the telemetry generated by other sources - such as [.Net Web SDK](https://github.com/Microsoft/ApplicationInsights-dotnet-server/) etc. or generated by the user directly.

## `Microsoft.ApplicationInsights.ServiceFabric`- For Service Fabric Lift and Shift Scenarios
You can deploy almost any existing application to Service Fabric as either a guest executable or guest container service. These are also sometimes referred to as lift and shift applications. Add the `Microsoft.ApplciationInsights.ServiceFabric` nuget to your guest executable / container services. 

### .Net Framework
For.Net applications, applying the nuget package will automatically add the telemetry initializer to the `ApplicationInsights.config` file. The following sample shows the new entry added in context of other entries added by other AI SDK nugets.

```
<?xml version="1.0" encoding="utf-8"?>
<ApplicationInsights xmlns="http://schemas.microsoft.com/ApplicationInsights/2013/Settings">
  <InstrumentationKey>...</InstrumentationKey>
  <TelemetryInitializers>
    <Add Type="Microsoft.ApplicationInsights.DependencyCollector.HttpDependenciesParsingTelemetryInitializer, Microsoft.AI.DependencyCollector"/>

<!-- **************************************************************************************************************** -->
    <Add Type="Microsoft.ApplicationInsights.ServiceFabric.FabricTelemetryInitializer, Microsoft.AI.ServiceFabric"/>
<!-- **************************************************************************************************************** -->

  </TelemetryInitializers>
  <TelemetryModules>
    <Add Type="Microsoft.ApplicationInsights.DependencyCollector.DependencyTrackingTelemetryModule, Microsoft.AI.DependencyCollector" />
  </TelemetryModules>
</ApplicationInsights>
```
 
### .Net Core
The AI .Net Core SDK's configuration model is quite different from .Net framework's AI SDK. Almost all configuration for .NET Core is done in code. For example the AI SDK for ASP.net Core provides UseApplicationInsights() utility method that lets you set things up in code. When using the service fabric specific nuget package, simply make sure to register the ServiceFabricTelemetryInitializer through dependency injection before calling UseApplicationInsights() method as shown below: 

```
public static void Main(string[] args)
{
    var host = new WebHostBuilder()
        .UseKestrel()
        
        // Adding Service Fabric Telemetry Initializer
        .ConfigureServices(services => services.AddSingleton<ITelemetryInitializer>((serviceProvider) => new FabricTelemetryInitializer()))
        
        .UseContentRoot(Directory.GetCurrentDirectory())
        .UseIISIntegration()
        .UseStartup<Startup>()

        // Configuring Applciation Insights
        .UseApplicationInsights()
        
        .Build();

    host.Run();
}

``` 

The nuget package reads the context from environment variables provided to the guest executable / guest container. The context added looks like the following:

![Context fields for guest containers as shown on application insights portal][context-screenshot-for-containers]

## `Microsoft.ApplicationInsights.ServiceFabric.Native` - For Service Fabric Reliable Services
Using reliable services framework can give the user, among other things, super high resources density, where a single process may host multiple microservices or even multiple instances of a single service. This is different from lift and shift scenarios where likely a single container instance is running an instance of a single microservice. `Microsoft.ApplciationInsights.ServiceFabric.Native` relies on `Microsoft.ApplicationInsights.ServiceFabric` nuget but provides additional utility methods that let you propagate the context from the [ServiceContext](https://docs.microsoft.com/en-us/dotnet/api/system.fabric.servicecontext?view=servicefabric-5.5.216) object to the telemetry initializer. 

### .Net Framework
Because `Microsoft.ApplicationInsights.ServiceFabric.Native` package installs the `Microsoft.ApplicationInsights.ServiceFabric` package as dependency, the telemetry initializer would already to registered in the ApplicationInsights.config file as described in the section for `Microsoft.ApplicationInsights.ServiceFabric` package above. In your service entry points, you should use the `FabricTelemetryInitializerExtension.SetServiceCallContext(ServiceContext)` provided by `Microsoft.ApplicationInsights.ServiceFabric.Native` package. This will make sure that desired context is retrieved from the `ServiceContext` object is propagated through all threads spawned from the service entry-point forth and is added to the outgoing telemetry.

```
protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
{
    FabricTelemetryInitializerExtension.SetServiceCallContext(this.Context);

    return new ServiceInstanceListener[]
    {
        new ServiceInstanceListener(serviceContext => new OwinCommunicationListener(Startup.ConfigureApp, serviceContext, ServiceEventSource.Current, "ServiceEndpoint"))
    };
}
```

-OR-

```
protected override Task RunAsync(CancellationToken cancellationToken)
{
    FabricTelemetryInitializerExtension.SetServiceCallContext(this.Context);

    return base.RunAsync(cancellationToken);
}
```

### .Net Core
For .net Core, simply create the telemetry initializer using the `FabricTelemetryInitializerExtension.CreateFabricTelemetryInitializer(ServiceContext)`and register it as an `ITelemetyInitializer` for dependency injection before any calls that configure the applciation insights pipeline.

```
protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
{
    return new ServiceInstanceListener[]
    {
        new ServiceInstanceListener(serviceContext =>
            new WebListenerCommunicationListener(serviceContext, "ServiceEndpoint", url =>
            {
                ServiceEventSource.Current.ServiceMessage(serviceContext, $"Starting WebListener on {url}");

                return new WebHostBuilder().UseWebListener()
                            .ConfigureServices(
                                services => services
                                    .AddSingleton<StatelessServiceContext>(serviceContext)
                                    .AddSingleton<ITelemetryInitializer>((serviceProvider) => FabricTelemetryInitializerExtension.CreateFabricTelemetryInitializer(serviceContext)))
                            .UseContentRoot(Directory.GetCurrentDirectory())
                            .UseStartup<Startup>()
                            .UseApplicationInsights()
                            .UseUrls(url)
                            .Build();
            }))
    };

}

```
The context added looks like: 
![Context fields for reliable services as shown on application insights portal][context-screenshot-for-native-services]

**Note:** `Cloud role name` and `Cloud role instance` shown in the screenshot above, represent service and service instance respectively, and are special fields in Application Insights schema that are used as aggregation units to power certain experiences like Application map.  

### Trace Correlation with Service Remoting
The Nuget package enables correlation of traces produced by Service Fabric services, regardless whether services communicate via HTTP or via Service Remoting protocol. For HttpClient, the correlation is supported implicitly. For Service Remoting, you just need to make some minor changes to your existing code and the correlation will be supported.
1. For the service invoking the request, change how the proxy is created:
    ```
    // IStatelessBackendService proxy = ServiceProxy.Create<IStatelessBackendService>(new Uri(serviceUri));
    var proxyFactory = new CorrelatingServiceProxyFactory(this.serviceContext, callbackClient => new FabricTransportServiceRemotingClientFactory(callbackClient: callbackClient));
    IStatelessBackendService proxy = proxyFactory.CreateServiceProxy<IStatelessBackendService>(new Uri(serviceUri));
    ```
2. For the service receiving the request, add a message handler to the service listener:
    ```
    protected override IEnumerable<ServiceInstanceListener> CreateServiceInstanceListeners()
    {
        return new ServiceInstanceListener[1]
        {
            new ServiceInstanceListener(context => new FabricTransportServiceRemotingListener(context, new CorrelatingRemotingMessageHandler(context, this)))
        };
    }
    ```

## Customizing Context:
< to be added >

## Branches
* `master` contains the latest published release located on NuGet.
* `develop` contains code for the next release.

## Contributing
### Report Issues 
Please file bugs, discussion or any other interesting topics in [issues](https://github.com/Microsoft/ApplicationInsights-servicefabric/issues).

### Developing
We strongly encourage contributions.   
This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.
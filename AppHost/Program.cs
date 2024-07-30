var builder = DistributedApplication.CreateBuilder(args);

_ = builder.AddProject<Projects.MyWeatherHub>("webfrontend").WithExternalHttpEndpoints().WithReference(builder.AddProject<Projects.Api>("apiservice"));

builder.Build().Run();

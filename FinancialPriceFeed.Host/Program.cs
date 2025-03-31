var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.FinancialPriceFeed>("financialpricefeed");

builder.Build().Run();

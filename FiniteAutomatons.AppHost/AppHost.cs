var builder = DistributedApplication.CreateBuilder(args);

var password = builder.AddParameter("password", "myStong_Password123#");

var db = builder.AddSqlServer("sql", password)
    .AddDatabase("finiteautomatonsdb");

var _ = builder.AddContainer("seq", "datalust/seq")
     .WithEnvironment("ACCEPT_EULA", "Y")
     .WithEnvironment("SEQ_FIRSTRUN_ADMINPASSWORD", password)
     .WithHttpEndpoint(port: 5341, targetPort: 80);

builder.AddProject<Projects.FiniteAutomatons>("finiteautomatons")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();

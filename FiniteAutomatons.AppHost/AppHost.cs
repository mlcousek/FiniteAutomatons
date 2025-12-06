var builder = DistributedApplication.CreateBuilder(args);

var password = builder.AddParameter("password", "myStong_Password123#");

var db = builder.AddSqlServer("sql", password)
    .AddDatabase("finiteautomatonsdb");

builder.AddProject<Projects.FiniteAutomatons>("finiteautomatons")
    .WithReference(db)
    .WaitFor(db);

builder.Build().Run();

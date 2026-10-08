using MasterNet.Application;
using MasterNet.Application.Interfaces;
using MasterNet.Domain.Courses;
using MasterNet.Infrastructure;
using MasterNet.Infrastructure.Photos;
using MasterNet.Infrastructure.Reports;
using MasterNet.Persistence;
using MasterNet.WebApi.Extensions;
using MasterNet.WebApi.Middleware;
using Microsoft.AspNetCore.OData;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

var builder = WebApplication.CreateBuilder(args);

//
//var connectionString = builder.Configuration.GetConnectionString("SqlServerDatabase");

//var sqlBuilder =
//    new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);

//Console.WriteLine("====================================");
//Console.WriteLine($"SQL SERVER: {sqlBuilder.DataSource}");
//Console.WriteLine($"DATABASE:   {sqlBuilder.InitialCatalog}");
//Console.WriteLine("====================================");

//

builder.AddSqlServerDbContext<MasterNetDbContext>(connectionName:"MasterNetDB");
builder.Services.AddHealthChecks();

builder.Services.AddApplication();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddInfrastructure();
builder.Services.AddIdentityServices(builder.Configuration);

builder.Services.AddPoliciesServices();

builder.Services
    .Configure<CloudinarySettings>(builder.Configuration.GetSection(nameof(CloudinarySettings)));

builder.Services.AddScoped<IPhotoService, PhotoService>();

builder.Services.AddScoped(typeof(IReportService<>), typeof(ReportService<>));

builder.AddServiceDefaults();

builder.Services.AddHttpContextAccessor();
IEdmModel GetEdmModel()
{
    var edm = new ODataConventionModelBuilder();
    edm.EntitySet<Course>("courses");
    return edm.GetEdmModel();
}

builder.Services.AddControllers()
                   .AddOData(opt => opt
                    .Select()
                    .Filter()
                    .OrderBy()
                    .Expand()
                    .Count()
                    .SetMaxTop(100)
                    .AddRouteComponents("odata", GetEdmModel())
                );
builder.Services.AddCors(o => o.AddPolicy("corsapp", builder =>
{
    builder.WithOrigins("*").AllowAnyMethod().AllowAnyHeader();
}));

var app = builder.Build();
app.MapHealthChecks("/health");
app.UseMiddleware<ExceptionMiddleware>();

app.UseAuthentication();
app.UseAuthorization();

app.UseCors("corsapp");

//await app.SeedDataAuthentication();

app.MapControllers();
app.Run();
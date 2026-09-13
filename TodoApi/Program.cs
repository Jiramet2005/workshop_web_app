using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.OpenApi;


using Scalar.AspNetCore;
using TodoApi.Dtos;
using TodoApi.Models;
using TodoApi.Data;



var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddOpenApi(options =>
{
    options.AddDocumentTransformer((document, _, _) =>
    {
        document.Components ??= new Microsoft.OpenApi.OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\""
        };

        return Task.CompletedTask;
    });
});


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
));

var jwtkey = builder.Configuration["jwt:Key"];
builder.Services
.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;

})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["jwt:Issuer"],
        ValidAudience = builder.Configuration["jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtkey))
    };
});

builder.Services.AddAuthorization();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

var todoGroup = app.MapGroup("/api/todos").WithTags("Todos");

#region In-Memory Endpoints

// var todos = new List<TodoGetDto>
// {
//     new(1, "Learn Minimal API", false),
//     new(2, "Learn Vue", false),
//     new(3, "Build a web API", false),
//     new(4, "Jiramet sudlor", true),
// };

// todoGroup.MapGet("/", () =>Results.Ok(todos));

// todoGroup.MapGet("/{id}", (int id) =>
// {
//     var todo = todos.FirstOrDefault(t => t.id == id);

//     return todo is not null ? Results.Ok(todo) : Results.NotFound();

// });
// todoGroup.MapPost("/", (TodoPostDto dto) =>
// {
//     var nextid = todos.Count == 0 ? 1 : todos.Max(t => t.id) + 1;

//     var todo = new TodoGetDto(nextid, dto.Title, false);
//     todos.Add(todo);

//     return Results.Created($"/api/todos/{nextid}", todo);

// });
// todoGroup.MapPut("/{id}", (int id, TodoPutDto dto) =>
// {
//     try
//     {
//         var index = todos.FindIndex(t => t.id == id);
//         if (index == -1)
//         {
//             return Results.NotFound();
//         }

//         todos[index] = todos[index] with
//         {
//             Title = dto.Title,
//             IsCompleted = dto.IsCompleted
//         };
//         return Results.Ok(todos[index]);
//     }
//     catch (Exception ex)
//     {
//         return Results.Problem(ex.Message);
//     }
// });

// todoGroup.MapDelete("/{id}", (int id) =>
// {
//     try
//     {
//         var todo = todos.FirstOrDefault(t => t.id == id);
//         if (todo is null)
//         {
//             return Results.NotFound();
//         }

//         todos.Remove(todo);
//         return Results.NoContent();
//     }
//     catch (Exception ex)
//     {
//         return Results.Problem(ex.Message);
//     }
// });

#endregion

#region Database Endpoints
todoGroup.MapGet("/", async (AppDbContext db) =>
{
    var todos = await db.Todos.ToListAsync();

    var todoGetDtos = todos.Select(t =>
                         new TodoGetDto(
                            t.Id,
                            t.Title,
                            t.IsCOmpleted));

    return todos.Count == 0 ? Results.NotFound() : Results.Ok(todos);
})
.RequireAuthorization();


todoGroup.MapPost("/", async (AppDbContext db, TodoPostDto dto) =>
{
    var lastTodo = await db.Todos.OrderByDescending(t => t.Id).FirstOrDefaultAsync();
    var nextId = lastTodo is null ? 1 : lastTodo.Id + 1;
    var todo = new TodoItem
    {
        Id = nextId,
        Title = dto.Title,
        IsCOmpleted = false,
        CreatedAt = DateTime.UtcNow
    };

    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    var todoGetDto = new TodoGetDto(todo.Id, todo.Title, todo.IsCOmpleted);

    return Results.Created($"/{todo.Id}", todo);
})
.RequireAuthorization();

#endregion

#region Authentication Endpoints

app.MapPost("/api/Login", (LoginDto dto, IConfiguration configuration) =>
{
    if(dto.Username != "admin" || dto.Password != "password") return Results.Unauthorized();

    // Generate JWT token here
    var claims = new[]
    {
        new Claim(ClaimTypes.Name, dto.Username)
    };

    var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["jwt:Key"]));

    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

    var token = new JwtSecurityToken(
        issuer: configuration["jwt:Issuer"],
        audience: configuration["jwt:Audience"],
        claims: claims,
        expires: DateTime.UtcNow.AddDays(int.Parse(configuration["jwt:ExpireDays"])),
        signingCredentials: credentials
    );

    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);

    return Results.Ok(new {Token = tokenString});

  
}).WithTags("Authentication").WithName("Login")
.Produces<LoginResponseDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized);

#endregion
app.Run();
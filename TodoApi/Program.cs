using Microsoft.EntityFrameworkCore;

using TodoApi.Dtos;
using TodoApi.Models;
using TodoApi.Data;

var builder = WebApplication.CreateBuilder(args);
// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")
));


var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

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
    var todos = await db.TodoItems.ToListAsync();
    return todos.Count == 0 ? Results.NotFound() : Results.Ok(todos);
});

todoGroup.MapPost("/", async (AppDbContext db, TodoPostDto dto) =>
{
    var lastTodo = await db.TodoItems.OrderByDescending(t => t.Id).FirstOrDefaultAsync();
    var nextId = lastTodo is null ? 1 : lastTodo.Id + 1;

    var todo = new TodoItem
    {
        Id = nextId,
        Title = dto.Title,
        IsCOmpleted = false,
        CreatedAt = DateTime.UtcNow
    };

    db.TodoItems.Add(todo);
    await db.SaveChangesAsync();

    var todoGetDto = new TodoGetDto(todo.Id, todo.Title, todo.IsCOmpleted);

    return Results.Created($"/{todo.Id}", todo);
});

#endregion

app.Run();
namespace TodoApi.Dtos;

public record TodoGetDto(
    int id,
    string Title,
    bool IsCompleted
);

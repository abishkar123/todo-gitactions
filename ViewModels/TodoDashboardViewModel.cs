using TodoList.Models;

namespace TodoList.ViewModels;

public sealed class TodoDashboardViewModel
{
    public CreateTodoItemInputModel NewItem { get; init; } = new();

    public IReadOnlyList<TodoItem> Items { get; init; } = [];

    public int CompletedCount => Items.Count(item => item.IsCompleted);

    public int OpenCount => Items.Count - CompletedCount;

    public int CompletionRatePercentage =>
        Items.Count == 0
            ? 0
            : (int)Math.Round((double)CompletedCount / Items.Count * 100, MidpointRounding.AwayFromZero);
}

using Moq;
using NUnit.Framework;
using TodoList.Models;
using TodoList.Repositories;
using TodoList.Services;

namespace TodoList.Tests.Services;

[TestFixture]
public sealed class TodoItemServiceTests
{
    [Test]
    public async Task CreateAsync_TrimsTitle_AndCreatesIncompleteItem()
    {
        var repositoryMock = new Mock<ITodoItemRepository>(MockBehavior.Strict);
        TodoItem? capturedItem = null;

        repositoryMock
            .Setup(repository => repository.CreateAsync(It.IsAny<TodoItem>(), It.IsAny<CancellationToken>()))
            .Callback<TodoItem, CancellationToken>((item, _) => capturedItem = item)
            .Returns(Task.CompletedTask);

        var service = new TodoItemService(repositoryMock.Object);
        var before = DateTime.UtcNow;

        await service.CreateAsync(
            new CreateTodoItemInputModel
            {
                Title = "  Learn NUnit  "
            },
            CancellationToken.None);

        var after = DateTime.UtcNow;

        Assert.That(capturedItem, Is.Not.Null);
        Assert.That(capturedItem!.Title, Is.EqualTo("Learn NUnit"));
        Assert.That(capturedItem.IsCompleted, Is.False);
        Assert.That(capturedItem.CreatedAtUtc, Is.InRange(before, after));
        repositoryMock.Verify(repository => repository.CreateAsync(It.IsAny<TodoItem>(), It.IsAny<CancellationToken>()), Times.Once);
        repositoryMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task UpdateAsync_TrimsTitle_AndDelegatesToRepository()
    {
        var repositoryMock = new Mock<ITodoItemRepository>(MockBehavior.Strict);
        var input = new EditTodoItemInputModel
        {
            Id = "0123456789abcdef01234567",
            Title = "  Updated title  "
        };

        repositoryMock
            .Setup(repository => repository.UpdateTitleAsync(input.Id, "Updated title", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var service = new TodoItemService(repositoryMock.Object);

        var result = await service.UpdateAsync(input, CancellationToken.None);

        Assert.That(result, Is.True);
        repositoryMock.Verify(repository => repository.UpdateTitleAsync(input.Id, "Updated title", It.IsAny<CancellationToken>()), Times.Once);
        repositoryMock.VerifyNoOtherCalls();
    }

    [Test]
    public async Task GetDashboardItemsAsync_PrioritizesOpenItems_ThenNewestFirst()
    {
        var repositoryMock = new Mock<ITodoItemRepository>(MockBehavior.Strict);
        var now = DateTime.UtcNow;
        var items = new[]
        {
            new TodoItem { Id = "1", Title = "Completed recent", IsCompleted = true, CreatedAtUtc = now.AddMinutes(-5) },
            new TodoItem { Id = "2", Title = "Open older", IsCompleted = false, CreatedAtUtc = now.AddHours(-2) },
            new TodoItem { Id = "3", Title = "Open newest", IsCompleted = false, CreatedAtUtc = now.AddMinutes(-1) },
            new TodoItem { Id = "4", Title = "Completed oldest", IsCompleted = true, CreatedAtUtc = now.AddDays(-1) }
        };

        repositoryMock
            .Setup(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(items);

        var service = new TodoItemService(repositoryMock.Object);

        var result = await service.GetDashboardItemsAsync(CancellationToken.None);

        Assert.That(result.Select(item => item.Id), Is.EqualTo(new[] { "3", "2", "1", "4" }));
        repositoryMock.Verify(repository => repository.GetAllAsync(It.IsAny<CancellationToken>()), Times.Once);
        repositoryMock.VerifyNoOtherCalls();
    }
}

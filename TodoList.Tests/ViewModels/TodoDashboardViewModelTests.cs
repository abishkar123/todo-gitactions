using NUnit.Framework;
using TodoList.Models;
using TodoList.ViewModels;

namespace TodoList.Tests.ViewModels;

[TestFixture]
public sealed class TodoDashboardViewModelTests
{
    [Test]
    public void LatestCreatedAtUtc_ReturnsNewestTimestampAcrossAllItems()
    {
        var now = DateTime.UtcNow;
        var model = new TodoDashboardViewModel
        {
            Items =
            [
                new TodoItem { Id = "1", Title = "Open older", IsCompleted = false, CreatedAtUtc = now.AddHours(-2) },
                new TodoItem { Id = "2", Title = "Completed newest", IsCompleted = true, CreatedAtUtc = now.AddMinutes(-5) },
                new TodoItem { Id = "3", Title = "Open newer", IsCompleted = false, CreatedAtUtc = now.AddHours(-1) }
            ]
        };

        Assert.That(model.LatestCreatedAtUtc, Is.EqualTo(now.AddMinutes(-5)));
    }

    [Test]
    public void LatestCreatedAtUtc_ReturnsNullWhenThereAreNoItems()
    {
        var model = new TodoDashboardViewModel();

        Assert.That(model.LatestCreatedAtUtc, Is.Null);
    }
}

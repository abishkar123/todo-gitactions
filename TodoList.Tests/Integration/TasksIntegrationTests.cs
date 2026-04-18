using System.Collections.Concurrent;
using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NUnit.Framework;
using TodoList.Models;
using TodoList.Repositories;

namespace TodoList.Tests.Integration;

[TestFixture]
public sealed class TasksIntegrationTests
{
    [Test]
    public async Task Create_Post_AddsTask_AndRendersItOnDashboard()
    {
        await using var factory = new TodoListWebApplicationFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = true,
            BaseAddress = new Uri("https://localhost")
        });

        var getResponse = await client.GetAsync("/");
        Assert.That(getResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var antiforgeryToken = await ExtractAntiforgeryTokenAsync(getResponse);

        using var postResponse = await client.PostAsync(
            "/Tasks/Create",
            new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("__RequestVerificationToken", antiforgeryToken),
                new KeyValuePair<string, string>("NewItem.Title", "  Integration test task  ")
            ]));

        postResponse.EnsureSuccessStatusCode();

        var html = await postResponse.Content.ReadAsStringAsync();
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("Integration test task"));
            Assert.That(html, Does.Contain("Total tasks"));
            Assert.That(html, Does.Contain(">1<"));
        });
    }

    private static async Task<string> ExtractAntiforgeryTokenAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(
            html,
            "<input name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant);

        Assert.That(match.Success, Is.True, "Expected antiforgery token field in the response.");
        return match.Groups[1].Value;
    }
}

internal sealed class TodoListWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITodoItemRepository>();
            services.AddSingleton<ITodoItemRepository, InMemoryTodoItemRepository>();
        });
    }
}

internal sealed class InMemoryTodoItemRepository : ITodoItemRepository
{
    private readonly ConcurrentDictionary<string, TodoItem> _items = new();

    public Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken cancellationToken)
    {
        var items = _items.Values
            .OrderByDescending(item => item.CreatedAtUtc)
            .ToArray();

        return Task.FromResult<IReadOnlyList<TodoItem>>(items);
    }

    public Task<TodoItem?> GetByIdAsync(string id, CancellationToken cancellationToken)
    {
        _items.TryGetValue(id, out var item);
        return Task.FromResult(item);
    }

    public Task CreateAsync(TodoItem item, CancellationToken cancellationToken)
    {
        item.Id = Guid.NewGuid().ToString("N")[..24];
        _items[item.Id] = item;
        return Task.CompletedTask;
    }

    public Task<bool> UpdateTitleAsync(string id, string title, CancellationToken cancellationToken)
    {
        if (!_items.TryGetValue(id, out var item))
        {
            return Task.FromResult(false);
        }

        item.Title = title;
        return Task.FromResult(true);
    }

    public Task<bool> ToggleCompletedAsync(string id, CancellationToken cancellationToken)
    {
        if (!_items.TryGetValue(id, out var item))
        {
            return Task.FromResult(false);
        }

        item.IsCompleted = !item.IsCompleted;
        return Task.FromResult(true);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken cancellationToken)
    {
        return Task.FromResult(_items.TryRemove(id, out _));
    }
}

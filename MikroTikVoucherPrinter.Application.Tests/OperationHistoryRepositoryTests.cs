using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Lux.Platform.Abstractions.Models;
using MikroTikVoucherPrinter.Application.Services;
using Xunit;

namespace MikroTikVoucherPrinter.Application.Tests;

public class OperationHistoryRepositoryTests
{
    private readonly InMemoryOperationHistoryRepository _repository;

    public OperationHistoryRepositoryTests()
    {
        _repository = new InMemoryOperationHistoryRepository();
    }

    [Fact]
    public async Task SaveAndGetById_ReturnsOperation()
    {
        var operation = new FleetOperation { Name = "Test Op", Progress = new OperationProgress { TotalDevices = 5 } };
        await _repository.SaveAsync(operation);

        var retrieved = await _repository.GetByIdAsync(operation.Id);

        Assert.NotNull(retrieved);
        Assert.Equal(operation.Id, retrieved.Id);
        Assert.Equal("Test Op", retrieved.Name);
    }

    [Fact]
    public async Task GetAll_ReturnsAllOperations()
    {
        var op1 = new FleetOperation();
        var op2 = new FleetOperation();
        await _repository.SaveAsync(op1);
        await _repository.SaveAsync(op2);

        var all = await _repository.GetAllAsync();

        Assert.Equal(2, all.Count);
        Assert.Contains(all, o => o.Id == op1.Id);
        Assert.Contains(all, o => o.Id == op2.Id);
    }
}

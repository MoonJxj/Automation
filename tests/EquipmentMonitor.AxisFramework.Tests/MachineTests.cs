using EquipmentMonitor.Common;
using EquipmentMonitor.Core;
using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Enums;
using EquipmentMonitor.Core.Events;
using EquipmentMonitor.Core.Models;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

public class MachineTests
{
    [Fact]
    public async Task RefreshAsync_WhenAnyElementRunning_AggregatesRunning()
    {
        var ready = new FakeElement("A", ElementState.Ready);
        var running = new FakeElement("B", ElementState.Running);
        var machine = CreateMachine(ready, running);

        var result = await machine.RefreshAsync();

        result.IsSuccess.Should().BeTrue();
        machine.State.Should().Be(MachineState.Running);
        machine.LastStateChangedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RefreshAsync_WhenAnyElementError_AggregatesErrorAndRaisesEvent()
    {
        var ready = new FakeElement("A", ElementState.Ready);
        var error = new FakeElement("B", ElementState.Error);
        var machine = CreateMachine(ready, error);
        MachineStateChangedEventArgs? captured = null;
        machine.StateChanged += (_, args) => captured = args;

        await machine.RefreshAsync();

        machine.State.Should().Be(MachineState.Error);
        captured.Should().NotBeNull();
        captured!.NewState.Should().Be(MachineState.Error);
        captured.SourceElementId.Should().Be("B");
    }

    [Fact]
    public void GetElement_WhenTypeMatches_ReturnsTypedElement()
    {
        var axis = new FakeAxisElement("X");
        var machine = CreateMachine(axis);

        var result = machine.GetElement<FakeAxisElement>("X");

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(axis);
    }

    [Fact]
    public void GetElement_WhenTypeDoesNotMatch_ReturnsFailure()
    {
        var element = new FakeElement("X", ElementState.Ready);
        var machine = CreateMachine(element);

        var result = machine.GetElement<FakeAxisElement>("X");

        result.IsFailure.Should().BeTrue();
        result.ErrorCode.Should().Be("Machine.ElementTypeMismatch");
    }

    private static Machine CreateMachine(params IElement[] elements)
        => new(
            new MachineMetadata
            {
                MachineCode = "M1",
                Name = "测试机台"
            },
            elements);

    private class FakeElement : IElement
    {
        public FakeElement(string id, ElementState state)
        {
            ElementMetadata = new ElementMetadata
            {
                ElementId = id,
                ElementType = "Fake",
                Name = id
            };
            State = state;
        }

        public ElementMetadata ElementMetadata { get; }

        public ElementState State { get; set; }

        public bool IsInitialized { get; private set; }

        public Task<Result> InitializeAsync(CancellationToken cancellationToken = default)
        {
            IsInitialized = true;
            return Task.FromResult(Result.Ok());
        }

        public Task<Result> RefreshAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Ok());

        public Task<Result> ResetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(Result.Ok());
    }

    private sealed class FakeAxisElement : FakeElement
    {
        public FakeAxisElement(string id)
            : base(id, ElementState.Ready)
        {
        }
    }
}

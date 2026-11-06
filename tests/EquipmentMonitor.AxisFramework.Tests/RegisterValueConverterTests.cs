using EquipmentMonitor.Common;
using FluentAssertions;

namespace EquipmentMonitor.AxisFramework.Tests;

public class RegisterValueConverterTests
{
    [Fact]
    public void ToInt32_WithHighWordFirst_CombinesRegistersInBigEndianOrder()
    {
        var value = RegisterValueConverter.ToInt32([0x1234, 0x5678]);

        value.Should().Be(0x12345678);
    }

    [Fact]
    public void ToInt32_WithLowWordFirst_CombinesRegistersInLittleEndianOrder()
    {
        var value = RegisterValueConverter.ToInt32([0x5678, 0x1234], RegisterWordOrder.LowWordFirst);

        value.Should().Be(0x12345678);
    }

    [Fact]
    public void ToSingle_WithHighWordFirst_ReadsIeee754()
    {
        var registers = RegisterValueConverter.FromSingle(12.5f);

        var value = RegisterValueConverter.ToSingle(registers);

        value.Should().Be(12.5f);
    }

    [Fact]
    public void FromDouble_WithLowWordFirst_RoundTrips()
    {
        var registers = RegisterValueConverter.FromDouble(-1234.5678, RegisterWordOrder.LowWordFirst);

        var value = RegisterValueConverter.ToDouble(registers, RegisterWordOrder.LowWordFirst);

        value.Should().Be(-1234.5678);
    }

    [Theory]
    [InlineData(false, 0x12)]
    [InlineData(true, 0x34)]
    public void ToByte_SelectsRequestedHalfOfRegister(bool highByte, byte expected)
    {
        const ushort word = 0x3412;

        RegisterValueConverter.ToByte(word, highByte).Should().Be(expected);
    }

    [Fact]
    public void FromValue_WithUnsupportedType_Throws()
    {
        var act = () => RegisterValueConverter.FromValue(DateTime.UtcNow);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void ToValue_WithWrongRegisterCount_Throws()
    {
        var act = () => RegisterValueConverter.ToInt32([0x0001]);

        act.Should().Throw<ArgumentException>();
    }
}

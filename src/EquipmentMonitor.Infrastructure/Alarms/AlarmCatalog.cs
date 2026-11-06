using EquipmentMonitor.Core.Abstractions;
using EquipmentMonitor.Core.Models;

namespace EquipmentMonitor.Infrastructure.Alarms;

/// <summary>
/// 报警码表：合并全部 <see cref="IAlarmDefinitionProvider"/> 的定义，未登记的码回退为通用文本。
/// </summary>
/// <remarks>
/// <para>
/// 合并顺序按 DI 注册顺序，<b>后注册覆盖先注册</b>：现场可以用配置 provider 覆盖内置默认值。
/// </para>
/// <para>
/// 未定义的码永不返回 <c>null</c>、永不返回空串——现场排查时「报警码 N」比空白有用。
/// </para>
/// </remarks>
public sealed class AlarmCatalog : IAlarmCatalog
{
    private readonly IReadOnlyDictionary<string, AlarmDefinition> _definitions;

    /// <summary>创建码表。</summary>
    /// <param name="providers">各设备/系统提供的码表。</param>
    /// <exception cref="InvalidOperationException">某个定义为空或缺少 Code。</exception>
    public AlarmCatalog(IEnumerable<IAlarmDefinitionProvider> providers)
    {
        ArgumentNullException.ThrowIfNull(providers);

        var definitions = new Dictionary<string, AlarmDefinition>(StringComparer.Ordinal);

        foreach (var provider in providers)
        {
            foreach (var definition in provider.GetDefinitions())
            {
                if (definition is null || string.IsNullOrWhiteSpace(definition.Code))
                {
                    throw new InvalidOperationException(
                        $"报警码表 {provider.GetType().Name} 提供了缺少 Code 的定义。");
                }

                definitions[definition.Code] = definition;
            }
        }

        _definitions = definitions;
    }

    /// <summary>已合并的码表（只读，测试与诊断用）。</summary>
    public IReadOnlyDictionary<string, AlarmDefinition> Definitions => _definitions;

    /// <inheritdoc />
    public AlarmDefinition Resolve(AlarmSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);

        if (_definitions.TryGetValue(signal.Code, out var definition))
        {
            return definition;
        }

        // 未注册的码：等级用上报值，文本保底，永不空串（延续旧 ServoAlarmTexts 规则）。
        return new AlarmDefinition
        {
            Code = signal.Code,
            Level = signal.Level,
            Title = "未定义报警",
            MessageTemplate = "{SourceId} 报警 {Code}（原始码 {RawCode}）{Detail}",
            Remedy = "该码尚未登记，请补充报警码表。"
        };
    }
}

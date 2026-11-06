using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 守卫：Designer 是静态布局的唯一来源，code-behind 不得再写坐标。
/// </summary>
/// <remarks>
/// <para>这次 <c>AxisOperationPanel.UpdateModePanels()</c> 在运行期改停止/错误/提示的 <c>Top</c>，
/// 导致 Designer 里怎么调都被覆盖。本测试扫描每个窗体/控件的 code-behind，
/// 只要出现“Designer 字段 + 坐标赋值”就失败。</para>
/// <para>动态生成控件的局部变量（如参数编辑器的 <c>control.Width</c>）不在 Designer 字段里，不受影响。</para>
/// </remarks>
public sealed class LayoutConventionTests
{
    private static readonly string[] LayoutProperties =
    [
        "Top", "Left", "Location", "Size", "Width", "Height", "Bounds"
    ];

    [Fact]
    public void 代码不得写Designer控件坐标()
    {
        var problems = new List<string>();
        var projectDir = Path.Combine(RepoRoot(), "src", "EquipmentMonitor.Desktop");

        foreach (var folder in new[] { "Forms", "Controls" })
        {
            var folderPath = Path.Combine(projectDir, folder);

            if (!Directory.Exists(folderPath))
            {
                continue;
            }

            foreach (var codeFile in Directory.GetFiles(folderPath, "*.cs")
                         .Where(file => !file.EndsWith(".Designer.cs", StringComparison.OrdinalIgnoreCase)))
            {
                var code = File.ReadAllText(codeFile);
                var match = Regex.Match(code, @"partial\s+class\s+(\w+)\s*:\s*(Form|UserControl)");

                if (!match.Success)
                {
                    continue;
                }

                var name = match.Groups[1].Value;
                var designerPath = Path.Combine(folderPath, name + ".Designer.cs");

                if (!File.Exists(designerPath))
                {
                    continue;
                }

                var designer = File.ReadAllText(designerPath);
                var lines = File.ReadAllLines(codeFile);

                foreach (Match field in Regex.Matches(designer, @"private\s+[\w\.]+\s+(_\w+)\s*(?:=|;)"))
                {
                    var fieldName = field.Groups[1].Value;
                    var pattern = new Regex(
                        $@"(?:this\s*\.\s*)?{Regex.Escape(fieldName)}\s*\.\s*(?:{string.Join("|", LayoutProperties)})\s*=",
                        RegexOptions.CultureInvariant);

                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (pattern.IsMatch(lines[i]))
                        {
                            problems.Add(
                                $"{folder}\\{name}.cs:{i + 1}: {fieldName} 写了坐标；" +
                                "静态布局只能放在 .Designer.cs，代码只切 Visible/Enabled/Dock/Anchor");
                        }
                    }
                }
            }
        }

        problems.Should().BeEmpty(
            "Designer 是布局的唯一来源；请按 .dsh/skills/winforms-designer 修正：{0}",
            string.Join("；", problems));
    }

    private static string RepoRoot([CallerFilePath] string path = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));
}

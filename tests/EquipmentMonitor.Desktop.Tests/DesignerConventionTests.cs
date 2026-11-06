using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using FluentAssertions;

namespace EquipmentMonitor.Desktop.Tests;

/// <summary>
/// 守卫：窗体/控件的分部类约定必须保持，否则 VS 设计器会再次报 CS0102 / CS0111 / CS0229。
/// </summary>
/// <remarks>
/// 不依赖人工检查：每次 <c>dotnet test</c> 都会扫描 <c>Forms/</c> 与 <c>Controls/</c>。
/// </remarks>
public sealed class DesignerConventionTests
{
    [Fact]
    public void 窗体与控件必须成对并遵守分部类约定()
    {
        var problems = new List<string>();
        var projectDir = Path.Combine(RepoRoot(), "src", "EquipmentMonitor.Desktop");
        var csproj = File.ReadAllText(Path.Combine(projectDir, "EquipmentMonitor.Desktop.csproj"));

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
                var subType = match.Groups[2].Value;
                var designerPath = Path.Combine(folderPath, name + ".Designer.cs");

                if (!File.Exists(designerPath))
                {
                    problems.Add($"{name}: 缺少 {name}.Designer.cs");
                    continue;
                }

                var designer = File.ReadAllText(designerPath);

                if (Regex.Matches(designer, @"\bvoid\s+InitializeComponent\s*\(").Count != 1)
                {
                    problems.Add($"{name}.Designer.cs: 必须且只能有一个 InitializeComponent");
                }

                if (Regex.IsMatch(code, @"\bvoid\s+InitializeComponent\s*\("))
                {
                    problems.Add($"{name}.cs: 不得再定义 InitializeComponent（CS0111）");
                }

                if (Regex.IsMatch(designer, @"private\s+readonly\s+[\w\.]+\s+_\w+"))
                {
                    problems.Add($"{name}.Designer.cs: 控件字段不能是 readonly");
                }

                foreach (Match field in Regex.Matches(designer, @"private\s+[\w\.]+\s+(_\w+)\s*(?:=|;)"))
                {
                    var fieldName = field.Groups[1].Value;

                    if (Regex.IsMatch(
                            code,
                            $@"\b(?:private|internal|protected|public)\s+(?:readonly\s+)?[\w\.]+\s+{Regex.Escape(fieldName)}\s*(?:=|;)"))
                    {
                        problems.Add($"{name}.cs: 重复声明控件字段 {fieldName}（CS0102）");
                    }
                }

                if (!Regex.IsMatch(code, $@"public\s+{Regex.Escape(name)}\s*\(\s*\)"))
                {
                    problems.Add($"{name}: 缺少设计器要求的无参构造");
                }

                if (Regex.IsMatch(code, @"public\s+" + Regex.Escape(name) + @"\s*\(\s*\)\s*\{[^}]*GetRequiredService"))
                {
                    problems.Add($"{name}: 无参构造不得解析服务（设计器会执行它）");
                }

                var relativeCode = $@"{folder}\{name}.cs";
                var relativeDesigner = $@"{folder}\{name}.Designer.cs";

                if (!csproj.Contains(
                        $@"<Compile Update=""{relativeCode}""><SubType>{subType}</SubType>",
                        StringComparison.Ordinal))
                {
                    problems.Add($"{name}: csproj 缺少 SubType={subType}");
                }

                if (!csproj.Contains(
                        $@"<Compile Update=""{relativeDesigner}""><DependentUpon>{name}.cs</DependentUpon>",
                        StringComparison.Ordinal))
                {
                    problems.Add($"{name}: csproj 缺少 Designer 的 DependentUpon");
                }
            }
        }

        problems.Should().BeEmpty(
            "窗体/控件必须保持 VS 设计器可解析；请按 .dsh/skills/winforms-designer 修正：{0}",
            string.Join("；", problems));
    }

    private static string RepoRoot([CallerFilePath] string path = "")
        => Path.GetFullPath(Path.Combine(Path.GetDirectoryName(path)!, "..", ".."));
}

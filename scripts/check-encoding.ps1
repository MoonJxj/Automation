# 文本文件编码守卫：统一 UTF-8 with BOM（.json/.sh 例外，保持 UTF-8 无 BOM）。
# 用法：pwsh -File scripts/check-encoding.ps1
# 退出码：0 = 通过；1 = 存在不合规文件。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$strict = [System.Text.UTF8Encoding]::new($false, $true)
$extensions = @('.cs', '.csproj', '.slnx', '.props', '.md', '.ps1', '.xml', '.config')
$noBomAllowed = @('.json', '.sh')
$problems = @()
$warnings = @()

Get-ChildItem -Path $root -Recurse -File | Where-Object {
    $name = $_.Name
    $ext = $_.Extension.ToLowerInvariant()
    ($extensions -contains $ext -or $name -eq '.editorconfig') -and
    $_.FullName -notmatch '\\(bin|obj|\.vs|\.tmp|\.acl-recovery)\\' -and
    $_.FullName -notmatch '\\node_modules\\'
} | ForEach-Object {
    $relative = $_.FullName.Replace($root + '\', '')
    $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
    $hasBom = $bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF
    $ext = $_.Extension.ToLowerInvariant()

    try {
        $text = $strict.GetString($bytes)
    }
    catch {
        $problems += "非法 UTF-8：$relative"
        return
    }

    if ($noBomAllowed -contains $ext) {
        if ($hasBom) { $warnings += "JSON/SH 不应带 BOM：$relative" }
    }
    elseif (-not $hasBom) {
        $problems += "缺少 UTF-8 BOM：$relative"
    }

    $replacementCount = ([regex]::Matches($text, [char]0xFFFD)).Count
    if ($replacementCount -gt 0) {
        $problems += "含 $replacementCount 个替换字符（内容已损坏）：$relative"
    }
}

if ($warnings.Count -gt 0) {
    $warnings | ForEach-Object { Write-Warning $_ }
}

if ($problems.Count -gt 0) {
    Write-Output '编码检查失败：'
    $problems | ForEach-Object { Write-Output "  [FAIL] $_" }
    exit 1
}

Write-Output '编码检查通过：源码/项目/文档均为 UTF-8 with BOM（.json/.sh 为 UTF-8 无 BOM）。'

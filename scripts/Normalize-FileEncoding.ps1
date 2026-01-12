# ======================================================================
# 文件编码统一脚本
# 功能：将项目中所有文本文件统一为 UTF-8 无 BOM 编码
# ======================================================================

$ErrorActionPreference = "Stop"

# 配置
$rootPath = "C:\Code\Ling\Ling.Mapper"
$fileExtensions = @("*.cs", "*.csproj", "*.md", "*.txt", "*.json", "*.xml", "*.yml", "*.yaml")
$excludeFolders = @("bin", "obj", ".vs", ".git", "packages", "node_modules")

# 统计
$totalFiles = 0
$convertedFiles = 0
$skippedFiles = 0
$errorFiles = 0

# 颜色输出
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

# 检测文件编码
function Get-FileEncoding {
    param([string]$Path)
    
    $bytes = [byte[]](Get-Content $Path -Encoding Byte -ReadCount 4 -TotalCount 4)
    
    if (!$bytes) { return "ASCII" }
    
    # BOM 检测
    if ($bytes[0] -eq 0xef -and $bytes[1] -eq 0xbb -and $bytes[2] -eq 0xbf) {
        return "UTF8-BOM"
    }
    elseif ($bytes[0] -eq 0xff -and $bytes[1] -eq 0xfe) {
        return "UTF16-LE"
    }
    elseif ($bytes[0] -eq 0xfe -and $bytes[1] -eq 0xff) {
        return "UTF16-BE"
    }
    else {
        return "UTF8-NoBOM"
    }
}

# 转换文件为 UTF-8 无 BOM
function Convert-ToUTF8NoBOM {
    param([string]$Path)
    
    try {
        # 读取文件内容
        $content = Get-Content $Path -Raw -Encoding UTF8
        
        # 写入为 UTF-8 无 BOM
        $utf8NoBom = New-Object System.Text.UTF8Encoding $false
        [System.IO.File]::WriteAllText($Path, $content, $utf8NoBom)
        
        return $true
    }
    catch {
        Write-ColorOutput "  错误: $($_.Exception.Message)" "Red"
        return $false
    }
}

# 主函数
function Main {
    Write-ColorOutput "`n========================================" "Cyan"
    Write-ColorOutput "文件编码统一工具" "Cyan"
    Write-ColorOutput "========================================`n" "Cyan"
    
    Write-ColorOutput "工作目录: $rootPath" "Gray"
    Write-ColorOutput "文件类型: $($fileExtensions -join ', ')" "Gray"
    Write-ColorOutput "排除文件夹: $($excludeFolders -join ', ')`n" "Gray"
    
    Write-ColorOutput "开始扫描文件..." "Yellow"
    
    # 获取所有文件
    $files = Get-ChildItem -Path $rootPath -Recurse -Include $fileExtensions | 
        Where-Object { 
            $file = $_
            $shouldExclude = $false
            foreach ($exclude in $excludeFolders) {
                if ($file.FullName -like "*\$exclude\*") {
                    $shouldExclude = $true
                    break
                }
            }
            -not $shouldExclude
        }
    
    $totalFiles = $files.Count
    Write-ColorOutput "找到 $totalFiles 个文件需要检查`n" "Green"
    
    # 处理每个文件
    $currentFile = 0
    foreach ($file in $files) {
        $currentFile++
        $relativePath = $file.FullName.Replace($rootPath, "").TrimStart("\")
        
        Write-Progress -Activity "处理文件" -Status "$currentFile / $totalFiles" -PercentComplete (($currentFile / $totalFiles) * 100)
        
        # 检测编码
        $encoding = Get-FileEncoding -Path $file.FullName
        
        if ($encoding -eq "UTF8-NoBOM") {
            Write-ColorOutput "  ? [$currentFile/$totalFiles] $relativePath" "Gray"
            $skippedFiles++
        }
        elseif ($encoding -eq "UTF8-BOM") {
            Write-ColorOutput "  → [$currentFile/$totalFiles] $relativePath (UTF-8 BOM → UTF-8)" "Yellow"
            if (Convert-ToUTF8NoBOM -Path $file.FullName) {
                Write-ColorOutput "    ? 转换成功" "Green"
                $convertedFiles++
            }
            else {
                $errorFiles++
            }
        }
        else {
            Write-ColorOutput "  → [$currentFile/$totalFiles] $relativePath ($encoding → UTF-8)" "Yellow"
            if (Convert-ToUTF8NoBOM -Path $file.FullName) {
                Write-ColorOutput "    ? 转换成功" "Green"
                $convertedFiles++
            }
            else {
                $errorFiles++
            }
        }
    }
    
    Write-Progress -Activity "处理文件" -Completed
    
    # 输出统计
    Write-ColorOutput "`n========================================" "Cyan"
    Write-ColorOutput "处理完成" "Cyan"
    Write-ColorOutput "========================================" "Cyan"
    Write-ColorOutput "总文件数:     $totalFiles" "White"
    Write-ColorOutput "已是 UTF-8:   $skippedFiles" "Gray"
    Write-ColorOutput "已转换:       $convertedFiles" "Green"
    Write-ColorOutput "转换失败:     $errorFiles" "Red"
    Write-ColorOutput "========================================`n" "Cyan"
    
    if ($convertedFiles -gt 0) {
        Write-ColorOutput "? 已成功转换 $convertedFiles 个文件为 UTF-8 无 BOM 编码" "Green"
    }
    
    if ($errorFiles -gt 0) {
        Write-ColorOutput "? $errorFiles 个文件转换失败，请检查日志" "Red"
    }
}

# 运行主函数
Main

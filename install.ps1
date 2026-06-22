# ============================================================================
# DocVault Enterprise - Windows Installation Script
# يقوم بالتثبيت التلقائي على Windows
# ============================================================================

# التحقق من أذونات المسؤول
if (-NOT ([Security.Principal.WindowsPrincipal] `
    [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(`
    [Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "❌ يجب تشغيل هذا السكريبت كمسؤول!" -ForegroundColor Red
    exit
}

Write-Host "`n════════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "DocVault Enterprise - Windows Installation" -ForegroundColor Green
Write-Host "════════════════════════════════════════════════════════════`n" -ForegroundColor Green

# ============================================================================
# تثبيت Chocolatey
# ============================================================================

Write-Host "📦 تثبيت Chocolatey..." -ForegroundColor Yellow

if (-not (Get-Command choco -ErrorAction SilentlyContinue)) {
    Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force

    [System.Net.ServicePointManager]::SecurityProtocol = `
        [System.Net.ServicePointManager]::SecurityProtocol -bor 3072

    iex ((New-Object System.Net.ServicePointManager).SecurityProtocol = `
        [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; `
        iwr https://community.chocolatey.org/install.ps1 -UseBasicParsing).Content

    Write-Host "✅ تم تثبيت Chocolatey" -ForegroundColor Green
} else {
    Write-Host "✅ Chocolatey مثبت بالفعل" -ForegroundColor Green
}

# ============================================================================
# تثبيت .NET 8 SDK
# ============================================================================

Write-Host "`n📦 تثبيت .NET 8 SDK..." -ForegroundColor Yellow

$dotnetVersion = dotnet --version 2>$null

if ($LASTEXITCODE -eq 0) {
    Write-Host "✅ .NET مثبت بالفعل: $dotnetVersion" -ForegroundColor Green
} else {
    choco install dotnet-8.0-sdk -y
    Write-Host "✅ تم تثبيت .NET 8" -ForegroundColor Green
}

# تحديث المتغيرات
$env:Path = [System.Environment]::GetEnvironmentVariable("Path","Machine") + ";" + `
            [System.Environment]::GetEnvironmentVariable("Path","User")

# ============================================================================
# تثبيت Docker Desktop
# ============================================================================

Write-Host "`n📦 تثبيت Docker Desktop..." -ForegroundColor Yellow

$installDocker = Read-Host "هل تريد تثبيت Docker Desktop؟ (y/n)"

if ($installDocker -eq "y") {
    choco install docker-desktop -y
    Write-Host "✅ تم تثبيت Docker Desktop" -ForegroundColor Green
    Write-Host "⚠️  يرجى إعادة تشغيل الكمبيوتر لإكمال التثبيت" -ForegroundColor Yellow
}

# ============================================================================
# تثبيت Git
# ============================================================================

Write-Host "`n📦 تثبيت Git..." -ForegroundColor Yellow

if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
    choco install git -y
    Write-Host "✅ تم تثبيت Git" -ForegroundColor Green
} else {
    Write-Host "✅ Git مثبت بالفعل" -ForegroundColor Green
}

# ============================================================================
# تثبيت Visual Studio Code (اختياري)
# ============================================================================

Write-Host "`n📦 تثبيت Visual Studio Code..." -ForegroundColor Yellow

$installVSCode = Read-Host "هل تريد تثبيت Visual Studio Code؟ (y/n)"

if ($installVSCode -eq "y") {
    choco install vscode -y
    Write-Host "✅ تم تثبيت Visual Studio Code" -ForegroundColor Green
}

# ============================================================================
# تنزيل مشروع DocVault
# ============================================================================

Write-Host "`n📥 تنزيل مشروع DocVault..." -ForegroundColor Yellow

$installDir = "C:\DocVault"

if (Test-Path $installDir) {
    Write-Host "📁 المجلد موجود بالفعل" -ForegroundColor Yellow
    $updateRepo = Read-Host "هل تريد تحديث المشروع؟ (y/n)"

    if ($updateRepo -eq "y") {
        cd $installDir
        git pull
    }
} else {
    mkdir $installDir | Out-Null
    cd $installDir
    git clone https://github.com/alwadi/docvault.git .
    Write-Host "✅ تم تنزيل المشروع" -ForegroundColor Green
}

# ============================================================================
# استعادة المراجع وبناء المشروع
# ============================================================================

Write-Host "`n🔨 بناء المشروع..." -ForegroundColor Yellow

cd $installDir

dotnet restore
dotnet publish -c Release -o "$installDir\publish"

Write-Host "✅ تم بناء المشروع" -ForegroundColor Green

# ============================================================================
# إنشاء اختصار على سطح المكتب
# ============================================================================

Write-Host "`n🎯 إنشاء اختصار..." -ForegroundColor Yellow

$desktopPath = [System.Environment]::GetFolderPath("Desktop")
$shortcutPath = "$desktopPath\DocVault.lnk"

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = "http://localhost:5000"
$shortcut.Description = "DocVault Enterprise"
$shortcut.Save()

Write-Host "✅ تم إنشاء الاختصار على سطح المكتب" -ForegroundColor Green

# ============================================================================
# معلومات التشغيل
# ============================================================================

Write-Host "`n════════════════════════════════════════════════════════════" -ForegroundColor Green
Write-Host "🎉 تم التثبيت بنجاح!" -ForegroundColor Green
Write-Host "════════════════════════════════════════════════════════════" -ForegroundColor Green

Write-Host "`n📊 معلومات التثبيت:" -ForegroundColor Cyan
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan
Write-Host "📁 مجلد التثبيت:      $installDir"
Write-Host "🌐 رابط التطبيق:      http://localhost:5000"
Write-Host "📦 المشروع:           Visual Studio Code / PowerShell"
Write-Host "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━" -ForegroundColor Cyan

Write-Host "`n${GREEN}الخطوات التالية:${NC}" -ForegroundColor Green
Write-Host "  1. فتح PowerShell"
Write-Host "  2. الانتقال إلى المجلد: cd C:\DocVault"
Write-Host "  3. تشغيل التطبيق:   dotnet run"
Write-Host "  4. فتح المتصفح:     http://localhost:5000"

Write-Host "`n💡 نصائح:" -ForegroundColor Yellow
Write-Host "  • لتشغيل أسرع:      dotnet run --configuration Release"
Write-Host "  • لتشغيل الاختبارات: dotnet test"
Write-Host "  • لـ Docker:        docker-compose up -d"

Write-Host "`n📞 للمساعدة:" -ForegroundColor Cyan
Write-Host "  📧 البريد: support@alwadi.ly"
Write-Host "  📚 التوثيق: https://docs.alwadi.ly"

Write-Host "`n"

# فتح المجلد
Start-Process explorer.exe -ArgumentList $installDir

param([string]$OutputDirectory=(Join-Path (Split-Path -Parent $PSScriptRoot) 'build\windows\app'))
$ErrorActionPreference='Stop'
$project=[IO.Path]::GetFullPath((Split-Path -Parent $PSScriptRoot))
$application=Join-Path $project 'src\Trazo.Windows'
$barcodeDirectory=Join-Path $project 'third_party\ZXing.Net\0.16.11'
$barcodeDll=Join-Path $barcodeDirectory 'zxing.dll'
if(-not(Test-Path -LiteralPath $barcodeDll -PathType Leaf)){throw 'Falta la dependencia incluida third_party\ZXing.Net\0.16.11\zxing.dll.'}
if((Get-FileHash -LiteralPath $barcodeDll -Algorithm SHA256).Hash -ne '52F00A43F9574A411009899A6236F3EFB5B597FBAE0F3163DFEC9CBB0C1F4D38'){throw 'La dependencia ZXing.Net no coincide con la versión fijada.'}
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if(-not(Test-Path -LiteralPath $compiler)){$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'}
if(-not(Test-Path -LiteralPath $compiler)){throw 'Se necesita .NET Framework 4.8 para compilar Trazo.'}
$sources=@(Get-ChildItem -LiteralPath $application -Filter '*.cs' -File -Recurse | Where-Object {$_.FullName -notmatch '[\\/](bin|obj)[\\/]'} | Sort-Object FullName | Select-Object -ExpandProperty FullName)
$release=[IO.Path]::GetFullPath($OutputDirectory)
$null=New-Item -ItemType Directory -Force -Path $release
$arguments=@('/nologo','/target:winexe','/optimize+','/platform:anycpu',('/win32manifest:'+ (Join-Path $application 'Properties\app.manifest')),('/win32icon:'+(Join-Path $project 'packaging\windows\Trazo.ico')),('/out:'+(Join-Path $release 'Trazo.exe')),'/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Xml.dll',('/reference:'+$barcodeDll))+$sources
& $compiler @arguments
if($LASTEXITCODE -ne 0){throw 'No se pudo compilar Trazo.'}
Copy-Item -LiteralPath (Join-Path $application 'Trazo.exe.config') -Destination (Join-Path $release 'Trazo.exe.config') -Force
Copy-Item -LiteralPath $barcodeDll -Destination (Join-Path $release 'zxing.dll') -Force
$licenseDirectory=Join-Path $release 'licenses\ZXing.Net'
$null=New-Item -ItemType Directory -Path $licenseDirectory -Force
foreach($name in @('LICENSE.txt','provenance.json')){Copy-Item -LiteralPath (Join-Path $barcodeDirectory $name) -Destination (Join-Path $licenseDirectory $name) -Force}
Write-Output ('Listo: '+(Join-Path $release 'Trazo.exe'))

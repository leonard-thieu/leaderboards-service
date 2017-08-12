[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$Assembly
)

if (-Not(Test-Path Env:\PROJECT)) { throw 'The environment variable "PROJECT" is not set. Tests will not be run.' }
$project = $env:PROJECT
$configuration = $env:CONFIGURATION
if ($configuration -eq $null) { $configuration = 'Debug' }

[xml]$xml = Get-Content "$project.Tests\packages.config"
$version = ($xml.packages.package | ? { $_.id -eq 'OpenCover' }).version

& "packages\OpenCover.$version\tools\OpenCover.Console.exe" `
    -register:user `
    -target:'vstest.console.exe' `
    "-targetargs:.\$project.Tests\bin\$configuration\$project.Tests.dll /logger:AppVeyor" `
    -filter:"+[$Assembly*]*"
if ($LASTEXITCODE -ne 0) { throw "Execution failed with exit code $LASTEXITCODE" }
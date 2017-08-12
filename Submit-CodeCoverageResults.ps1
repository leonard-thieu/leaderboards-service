if (Test-Path Env:\APPVEYOR_PULL_REQUEST_NUMBER) {
    $msg = 'The environment variable "COVERALLS_REPO_TOKEN" is a secure environment variable. ' + `
           'Secure environment variables are not available during pull request builds. ' + `
           'Code coverage results have not been submitted.'
    Write-Output $msg
} elseif (!(Test-Path Env:\COVERALLS_REPO_TOKEN)) {
    $msg = 'The environment variable "COVERALLS_REPO_TOKEN" is not set. ' + `
           'Code coverage results have not been submitted.'
    Write-Warning $msg
} else {
    [xml]$xml = Get-Content "$env:PROJECT.Tests\packages.config"
    $version = ($xml.packages.package | ? { $_.id -eq 'coveralls.io' }).version

    & "packages\coveralls.io.$version\tools\coveralls.net.exe" `
        --opencover results.xml `
        -r $env:COVERALLS_REPO_TOKEN
    if ($LASTEXITCODE -ne 0) { throw "Execution failed with exit code $LASTEXITCODE" }

    Write-Output 'Code coverage results have been submitted.'
}
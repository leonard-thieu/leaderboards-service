[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [String]$SteamUserName,
    [Parameter(Mandatory = $true)]
    [String]$SteamPassword,
    [String]$LeaderboardsConnectionString = 'Data Source=localhost;Initial Catalog=NecroDancer;Integrated Security=SSPI',
    [Switch]$Overwrite = $false
)

. .\CredMan.ps1

function ShouldWrite-Credentials {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [String]$Target
    )

    return ($Overwrite -eq $true) -or -not((Read-Creds -Target $Target) -is [PsUtils.CredMan+Credential])
}

function Write-Credentials {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [String]$Target,
        [Parameter(ParameterSetName = 'UserName')]
        [String]$UserName,
        [Parameter(Mandatory = $true)]
        [String]$Password
    )

    if (ShouldWrite-Credentials -Target $Target) {
        switch ($PsCmdlet.ParameterSetName) {
            'UserName' { $result = Write-Creds -Target $Target -UserName $UserName -Password $Password -CredPersist LOCAL_MACHINE }
            default { $result = Write-Creds -Target $Target -Password $Password -CredPersist LOCAL_MACHINE }
        }
        if ($result -eq 0) { 
            Write-Output "Credentials for '$Target' have been saved." 
        } else { 
            Write-Output "An error code of '$result' was returned when saving credentials for '$Target'."
        }    
    } else {
        Write-Output "Credentials for '$Target' exist and -Overwrite was not specified."
    }
}

Write-Credentials -Target 'toofz/Steam' -UserName $SteamUserName -Password $SteamPassword
Write-Credentials -Target 'toofz/LeaderboardsConnectionString' -Password $LeaderboardsConnectionString
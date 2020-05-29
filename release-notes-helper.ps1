$versionRegex = '(^## )((?:\[+)?v[0-9]+\.[0-9]+\.[0-9])'

function Get-ReleaseNotes {
  param( [string]$Version, [string]$ChangeLogPath )
  
  $sb = New-Object -TypeName "System.Text.StringBuilder"
  $isReleaseNotesFound = $false
  $currentVersionRegex = "(^## )((?:\[+)?$Version)"
  foreach ($line in Get-Content $ChangeLogPath) {
    if (!$isReleaseNotesFound -and $line -match $currentVersionRegex) {
      $isReleaseNotesFound = $true
    }
    elseif ($isReleaseNotesFound -and !($line -match $versionRegex)) {
      [void]$sb.AppendLine($line)
    }
    elseif ($isReleaseNotesFound) {
      break
    }
  }
  $releaseNotes = $sb.ToString()
  return $releaseNotes
}

function Submit-ReleaseNotesToGit {
  param ([string]$Version, [string]$ReleaseNotes, [string]$Repo, [string]$GithubToken)
  $headers = @{
    "Authorization" = "Bearer $GithubToken"
    "Content-Type"  = "application/json"
    "User-Agent"    = "AppVeyorDeployer"
  }

  $infoUrl = "https://api.github.com/repos/$Repo/releases/tags/$Version"

  Write-Output $infoUrl

  $releaseInfo = Invoke-WebRequest -Headers $headers -Uri $infoUrl | ConvertFrom-Json 

  $releaseId = $releaseInfo.id

  $postUrl = "https://api.github.com/repos/$Repo/releases/$releaseId"

  Write-Output $postUrl

  $releaseUpdate = @{ 
    "tag_name" = $Version
    "body"     = $ReleaseNotes
  } | ConvertTo-Json

  Invoke-RestMethod -Uri $postUrl -Headers $headers -Method 'POST' -Body $releaseUpdate
}

function Update-ReleaseNotes {
  param ([string]$Version, [string]$ChangeLogPath, [string]$Repo, [string]$GithubToken)
  $releaseNotes = Get-ReleaseNotes $Version $ChangeLogPath
  if ($releaseNotes.Length -lt 1) {
    return
  }
  Submit-ReleaseNotesToGit $Version $releaseNotes $Repo $GithubToken
}
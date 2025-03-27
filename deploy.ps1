# deploy.ps1 with logging to deploy.log

$logFile = "deploy.log"
Log ("Running as user: " + [System.Security.Principal.WindowsIdentity]::GetCurrent().Name)

function Log($msg) {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $line = "$timestamp  $msg"
    Write-Host $line
    Add-Content -Path $logFile -Value $line
}

$branch = $env:CI_COMMIT_BRANCH.ToLower()
Log "Current branch: $branch"

if ($branch -eq 'main') {
    $targetPath = "$env:DEPLOY_BASE_PATH\API_Production"
    $siteName = 'API_Production'
} elseif ($branch -eq 'develop') {
    $targetPath = "$env:DEPLOY_BASE_PATH\API_Test"
    $siteName = 'API_Test'
} else {
    Log "Unsupported branch: $branch"
    exit 1
}

Log "Target path: $targetPath"
Log "IIS site name: $siteName"

Import-Module WebAdministration

if (!(Test-Path ".\publish")) {
    Log "Publish folder not found."
    exit 1
}

Log "Listing publish directory:"
Get-ChildItem ".\publish" -Recurse | ForEach-Object { Log $_.FullName }

"Site is being updated, please wait..." | Out-File ".\publish\app_offline.htm" -Encoding UTF8

if (Test-Path ".\publish\app_offline.htm") {
    Log "Copying app_offline.htm to target folder..."
    Copy-Item -Path ".\publish\app_offline.htm" -Destination "$targetPath" -Force
} else {
    Log "app_offline.htm was not created. Skipping copy."
}

Log "Stopping IIS site..."
Stop-Website -Name $siteName

# ✅ 加入 IIS APPPOOL\<siteName> 權限
try {
    $appPoolUser = "IIS APPPOOL\$siteName"
    $acl = Get-Acl -Path $targetPath
    $rule = New-Object System.Security.AccessControl.FileSystemAccessRule($appPoolUser, "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($rule)
    Set-Acl -Path $targetPath -AclObject $acl
    Log "Granted ReadAndExecute permission to $appPoolUser"
} catch {
    Log "Failed to set permission for $appPoolUser"
    Log $_.Exception.Message
}

Log "Copying published files to target path..."
Copy-Item -Path ".\publish\*" -Destination "$targetPath" -Recurse -Force -Verbose | ForEach-Object { Log $_ }

Log "Starting IIS site..."
Start-Website -Name $siteName

if (Test-Path "$targetPath\app_offline.htm") {
    Log "Removing app_offline.htm"
    Remove-Item "$targetPath\app_offline.htm" -Force
} else {
    Log "app_offline.htm not found in target. Skipping removal."
}

Log "Verifying target path contents:"
Get-ChildItem "$targetPath" -Recurse | ForEach-Object { Log $_.FullName }

Log "Deployment completed."

try {
    $smtpServer = 'bdrelay.theil.com'
    $from = 'CIM.DEPLOY@theil.com'
    $to = 'silva.he@theil.com','max.yang@theil.com','julie.peng@theil.com'
    $subject = "API Deployment Success [$env:CI_COMMIT_BRANCH]"
    $body = @"
Deployment completed successfully.

Branch: $env:CI_COMMIT_BRANCH
Commit: $env:CI_COMMIT_SHORT_SHA
User: $env:GITLAB_USER_NAME
Pipeline: $env:CI_PIPELINE_URL
"@

    Send-MailMessage -From $from -To $to -Subject $subject -Body $body -SmtpServer $smtpServer
    Log "Email sent successfully."
} catch {
    Log "Email failed to send."
    Log $_.Exception.Message
}

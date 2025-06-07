# deploy.ps1 with logging to deploy.log

$logFile = "deploy.log"


function Log($msg) {
    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $line = "$timestamp  $msg"
    Write-Host $line
    Add-Content -Path $logFile -Value $line
}

Log ("Running as user: " + [System.Security.Principal.WindowsIdentity]::GetCurrent().Name)

#scan by DevKim

Write-Host "Checking DevSkim security scan result..."

$scanFile = "scan_result.sarif"

if (Test-Path $scanFile) {
    $scanContent = Get-Content $scanFile -Raw
    if ($scanContent -like '*"severity": "high"*') {
        Write-Host "High severity security issues found. Deployment aborted."
        $msg = "High severity security issues found. Deployment aborted."
        Log $msg
        exit 1
    } else {
        Write-Host "No high severity issues found. Proceeding with deployment..."
    	Log "No high severity issues found. Proceeding with deployment..."
    }
} else {
    Write-Host "Scan result file not found. Proceeding with deployment."
    Log "Scan result file not found. Proceeding with deployment."

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
Start-Sleep -Seconds 3


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

Log "Robocopy deploying files..."
$robocopyResult = robocopy ".\publish" "$targetPath" /MIR /Z /NP /R:3 /W:5 /LOG+:$logFile
Log "Robocopy result code: $robocopyResult"

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

    $attachment = "scan_report.html"
    if (Test-Path $attachment) {
        Send-MailMessage -From $from -To $to -Subject $subject -Body $body -SmtpServer $smtpServer -Attachments $attachment
        Log "Email sent with scan_report.html attached."
    } else {
        Send-MailMessage -From $from -To $to -Subject $subject -Body $body -SmtpServer $smtpServer
        Log "Email sent (no scan report found to attach)."
    }
} catch {
    Log "Email failed to send."
    Log $_.Exception.Message
}

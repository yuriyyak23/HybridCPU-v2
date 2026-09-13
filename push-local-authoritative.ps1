$ErrorActionPreference = "Stop"

$branch = git branch --show-current
if ($branch -ne "master") {
    throw "Expected local branch 'master', but found '$branch'."
}

$remote = git remote get-url origin
if ($remote -ne "https://github.com/yuriyyak23/HybridCPU-v2.git") {
    throw "Unexpected origin remote: $remote"
}

Write-Host "Force-pushing local master to origin/master..."
git -c credential.interactive=auto push origin master --force
if ($LASTEXITCODE -ne 0) {
    throw "git push failed with exit code $LASTEXITCODE."
}

Write-Host "Push completed."

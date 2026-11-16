$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
$tools=Join-Path $root 'Shared/tools/automation'
$run=& "$tools/create-run.ps1" -GameVersion 1.7 -SavePattern 'Fisso-NAM-1.7.rws' -Purpose 'Readouts: books held in bookcases versus published counts' -ModSet readouts-books -AdditionalModSource @("$PSScriptRoot/mod")
$out=Join-Path $root "Readouts/temp/books/$($run.runId)"
New-Item -ItemType Directory -Path $out -Force | Out-Null
try {
    & "$tools/launch.ps1" -RunId $run.runId -TimeoutMinutes 5 -HardCapSeconds 60 -AllowRecoverableSaveErrors
    $deadline=[DateTime]::UtcNow.AddSeconds(30)
    while(-not(Test-Path (Join-Path $run.profile 'books-done.txt'))) {
        if([DateTime]::UtcNow -gt $deadline){throw 'Books probe timed out.'}
        Start-Sleep -Milliseconds 100
    }
} finally {
    & "$tools/stop.ps1" -RunId $run.runId
    foreach($name in 'Player.log','run.json','launch.json','host.json','books-results.txt') {
        $path=Join-Path $run.profile $name
        if(Test-Path $path){Copy-Item -LiteralPath $path -Destination $out -Recurse}
    }
    & "$tools/remove-run.ps1" -RunId $run.runId
    Write-Output $out
}
$results=Get-Content (Join-Path $out 'books-results.txt')
$results
if(@($results -match '^(FAIL|ERROR)').Count -or -not @($results -match '^PASS').Count){throw 'Readouts books verification failed.'}

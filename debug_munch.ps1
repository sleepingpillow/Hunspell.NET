# Debug munch behavior
$tmpDir = New-TemporaryFile | %{ Remove-Item $_; New-Item -ItemType Directory -Path $_ }

try {
    $aff = Join-Path $tmpDir "test.aff"
    $dic = Join-Path $tmpDir "words.lst"

    @"
SFX A Y 1
SFX A 0 s .
"@ | Out-File -FilePath $aff -Encoding utf8

    @"
hund
hunds
"@ | Out-File -FilePath $dic -Encoding utf8

    Write-Host "=== Test files created ==="
    Write-Host "AFF: $aff"
    Get-Content $aff | Write-Host
    Write-Host "`nDIC: $dic"
    Get-Content $dic | Write-Host

    # Run the munch tool
    Write-Host "`n=== Running munch ==="
    dotnet run --project "src/Tools.Munch/Tools.Munch.csproj" -- $dic $aff

} finally {
    Remove-Item -Recurse -Force $tmpDir
}

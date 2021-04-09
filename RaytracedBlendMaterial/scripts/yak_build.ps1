Set-Location -Path "$PSScriptRoot\..\bin\$Args"

if (Test-Path "manifest.yml")
{
    Remove-Item "manifest.yml"
}

Get-ChildItem . -Recurse -Filter *.pdb | Remove-Item

& 'C:\Program Files\Rhino 8 WIP\System\Yak.exe' spec

& 'C:\Program Files\Rhino 8 WIP\System\Yak.exe' build
Push-Location $PSScriptRoot
try {
    dotnet build/build.cs -- $args
    exit $LASTEXITCODE
}
finally {
    Pop-Location
}

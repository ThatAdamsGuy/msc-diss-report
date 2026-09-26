param(
    [Parameter(Mandatory = $true)]
    [string]$ExportDirectory
)

dotnet run -- $ExportDirectory

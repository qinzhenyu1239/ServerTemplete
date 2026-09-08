[CmdletBinding()]
param(
    [string]$ConfigPath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($ConfigPath)) {
    $ConfigPath = Join-Path $PSScriptRoot 'LubanExcelToCsv.json'
}

function Get-NormalizedFieldName {
    param([AllowEmptyString()][string]$Name)

    if ($null -eq $Name) { return '' }
    return ([regex]::Replace($Name.ToLowerInvariant(), '[^a-z0-9]', ''))
}

function ConvertTo-CsvField {
    param([AllowNull()]$Value)

    if ($null -eq $Value) { return '' }
    $text = [string]$Value
    if ($text.Contains('"')) { $text = $text.Replace('"', '""') }
    if ($text.IndexOfAny([char[]]@(',', '"', "`r", "`n")) -ge 0) {
        return '"' + $text + '"'
    }
    return $text
}

function Read-CsvTemplate {
    param([string]$Path)

    Add-Type -AssemblyName Microsoft.VisualBasic
    $parser = [Microsoft.VisualBasic.FileIO.TextFieldParser]::new($Path)
    try {
        $parser.TextFieldType = [Microsoft.VisualBasic.FileIO.FieldType]::Delimited
        $parser.SetDelimiters(',')
        $parser.HasFieldsEnclosedInQuotes = $true
        $rows = @()
        for ($index = 0; $index -lt 3; $index++) {
            if ($parser.EndOfData) {
                throw "CSV template must contain description, type and field-name rows: $Path"
            }
            $rows += ,$parser.ReadFields()
        }
        return ,$rows
    }
    finally {
        $parser.Close()
    }
}

function Convert-CellValue {
    param(
        [AllowNull()]$Value,
        [string]$TargetType
    )

    $type = $TargetType.Trim().ToLowerInvariant()
    if ($null -eq $Value -or ([string]$Value).Length -eq 0) {
        if ($type -in @('int', 'long', 'float', 'double')) { return '0' }
        return ''
    }

    if ($Value -is [bool]) {
        if ($type -in @('int', 'long', 'float', 'double')) {
            return $(if ($Value) { '1' } else { '0' })
        }
        return $Value.ToString().ToUpperInvariant()
    }

    $culture = [System.Globalization.CultureInfo]::InvariantCulture
    switch ($type) {
        'int' { return ([Convert]::ToInt32($Value, $culture)).ToString($culture) }
        'long' { return ([Convert]::ToInt64($Value, $culture)).ToString($culture) }
        'float' { return ([Convert]::ToDouble($Value, $culture)).ToString('G15', $culture) }
        'double' { return ([Convert]::ToDouble($Value, $culture)).ToString('G15', $culture) }
        default { return [string]$Value }
    }
}

function Convert-LubanType {
    param([string]$TypeName)

    $type = $TypeName.Trim().ToLowerInvariant()
    if ($type -eq 'bool') { return 'int' }
    if ($type -in @('int', 'long', 'float', 'double', 'string')) { return $type }
    throw "Unsupported Luban field type: $TypeName"
}

function New-SpreadsheetApplication {
    foreach ($programId in @('Excel.Application', 'ket.Application')) {
        try {
            $application = New-Object -ComObject $programId
            Write-Host "Spreadsheet application: $programId"
            return $application
        }
        catch {
            continue
        }
    }
    throw 'Microsoft Excel or WPS Spreadsheets is required to convert XLSX files.'
}

$resolvedConfigPath = (Resolve-Path -LiteralPath $ConfigPath).Path
$config = Get-Content -LiteralPath $resolvedConfigPath -Raw -Encoding UTF8 | ConvertFrom-Json
$sourceDirectory = [System.IO.Path]::GetFullPath([string]$config.paths.sourceExcelDirectory)
$excelCopyDirectory = [System.IO.Path]::GetFullPath([string]$config.paths.excelCopyDirectory)
$csvOutputDirectory = [System.IO.Path]::GetFullPath([string]$config.paths.csvOutputDirectory)

if (-not (Test-Path -LiteralPath $sourceDirectory -PathType Container)) {
    throw "Source Excel directory does not exist: $sourceDirectory"
}
if (-not (Test-Path -LiteralPath $csvOutputDirectory -PathType Container)) {
    throw "CSV output directory does not exist: $csvOutputDirectory"
}
[System.IO.Directory]::CreateDirectory($excelCopyDirectory) | Out-Null

$enabledTables = @($config.tables | Where-Object { $_.enabled })
if ($enabledTables.Count -eq 0) {
    throw 'No enabled tables were found in the conversion configuration.'
}

$application = New-SpreadsheetApplication
$application.Visible = $false
$application.DisplayAlerts = $false

try {
    foreach ($table in $enabledTables) {
        $sourceWorkbook = Join-Path $sourceDirectory ([string]$table.workbook)
        $copiedWorkbook = Join-Path $excelCopyDirectory ([string]$table.workbook)
        $outputCsv = Join-Path $csvOutputDirectory ([string]$table.outputCsv)

        if (-not (Test-Path -LiteralPath $sourceWorkbook -PathType Leaf)) {
            throw "Source workbook does not exist for table '$($table.name)': $sourceWorkbook"
        }

        Copy-Item -LiteralPath $sourceWorkbook -Destination $copiedWorkbook -Force
        Write-Host "Copied: $sourceWorkbook -> $copiedWorkbook"

        $existingMetadata = @{}
        if (Test-Path -LiteralPath $outputCsv -PathType Leaf) {
            $templateRows = Read-CsvTemplate -Path $outputCsv
            $descriptions = $templateRows[0]
            $types = $templateRows[1]
            $fields = $templateRows[2]
            if ($descriptions.Count -ne $types.Count -or $types.Count -ne $fields.Count) {
                throw "The first three rows have different column counts: $outputCsv"
            }

            for ($index = 0; $index -lt $fields.Count; $index++) {
                $normalized = Get-NormalizedFieldName ([string]$fields[$index])
                $existingMetadata[$normalized] = [PSCustomObject]@{
                    Field = [string]$fields[$index]
                    Type = [string]$types[$index]
                    Description = [string]$descriptions[$index]
                }
            }
        }
        else {
            Write-Host "CSV template not found; creating a new file from workbook metadata: $outputCsv"
        }

        $workbook = $application.Workbooks.Open($copiedWorkbook, 0, $true)
        try {
            $worksheet = $workbook.Worksheets.Item([string]$table.worksheet)
            $seenSourceFields = @{}
            $outputDescriptions = @()
            $outputTypes = @()
            $outputFields = @()
            $columnMappings = @()
            for ($column = 1; $column -le $worksheet.UsedRange.Columns.Count; $column++) {
                $sourceName = [string]$worksheet.Cells.Item(1, $column).Text
                if ($sourceName -eq '##var' -or [string]::IsNullOrWhiteSpace($sourceName)) { continue }
                $normalized = Get-NormalizedFieldName $sourceName
                if ($seenSourceFields.ContainsKey($normalized)) {
                    throw "Duplicate source field '$sourceName' in workbook: $copiedWorkbook"
                }
                $seenSourceFields[$normalized] = $true

                $sourceType = Convert-LubanType ([string]$worksheet.Cells.Item(2, $column).Text)
                $sourceDescription = [string]$worksheet.Cells.Item(4, $column).Text
                if ($existingMetadata.ContainsKey($normalized)) {
                    $metadata = $existingMetadata[$normalized]
                    $outputFields += $metadata.Field
                    $outputTypes += $metadata.Type
                    $outputDescriptions += $(if ([string]::IsNullOrWhiteSpace($sourceDescription)) {
                        $metadata.Description
                    } else {
                        $sourceDescription
                    })
                }
                else {
                    $outputFields += $sourceName
                    $outputTypes += $sourceType
                    $outputDescriptions += $sourceDescription
                }
                $columnMappings += $column
            }

            $descriptions = $outputDescriptions
            $types = $outputTypes
            $fields = $outputFields
            $templateRows = @($descriptions, $types, $fields)

            $temporaryCsv = "$outputCsv.$([Guid]::NewGuid().ToString('N')).tmp"
            $writer = [System.IO.StreamWriter]::new(
                $temporaryCsv,
                $false,
                [System.Text.UTF8Encoding]::new($false))
            try {
                foreach ($headerRow in $templateRows) {
                    $writer.WriteLine((@($headerRow | ForEach-Object { ConvertTo-CsvField $_ }) -join ','))
                }

                $writtenRows = 0
                for ($row = 5; $row -le $worksheet.UsedRange.Rows.Count; $row++) {
                    $keyValue = $worksheet.Cells.Item($row, $columnMappings[0]).Value2
                    if ($null -eq $keyValue -or ([string]$keyValue).Trim().Length -eq 0) { continue }

                    $values = for ($index = 0; $index -lt $fields.Count; $index++) {
                        $value = $worksheet.Cells.Item($row, $columnMappings[$index]).Value2
                        ConvertTo-CsvField (Convert-CellValue -Value $value -TargetType ([string]$types[$index]))
                    }
                    $writer.WriteLine(($values -join ','))
                    $writtenRows++
                }
            }
            finally {
                $writer.Dispose()
            }

            $backupCsv = "$outputCsv.$([Guid]::NewGuid().ToString('N')).bak"
            try {
                if (Test-Path -LiteralPath $outputCsv -PathType Leaf) {
                    [System.IO.File]::Replace($temporaryCsv, $outputCsv, $backupCsv)
                }
                else {
                    [System.IO.File]::Move($temporaryCsv, $outputCsv)
                }
            }
            catch {
                if (Test-Path -LiteralPath $temporaryCsv) {
                    [System.IO.File]::Delete($temporaryCsv)
                }
                throw
            }
            finally {
                if (Test-Path -LiteralPath $backupCsv) {
                    [System.IO.File]::Delete($backupCsv)
                }
            }
            Write-Host "Converted: $copiedWorkbook -> $outputCsv ($writtenRows data rows)"
        }
        finally {
            if ($null -ne $workbook) {
                $workbook.Close($false)
                [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($workbook) | Out-Null
            }
        }
    }
}
finally {
    $application.Quit()
    [System.Runtime.InteropServices.Marshal]::FinalReleaseComObject($application) | Out-Null
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}

Write-Host "Completed $($enabledTables.Count) table(s)."

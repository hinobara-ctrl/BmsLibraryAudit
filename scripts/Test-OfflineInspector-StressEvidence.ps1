#requires -Version 7.0
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TrxPath,
    [Parameter(Mandatory)][string]$OutputJson
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../.artifacts/source-inspector-offline'))
function ArtifactPath([string]$path) {
    $absolute = [IO.Path]::GetFullPath($path)
    if (-not $absolute.StartsWith($artifactRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'ARTIFACT_SCOPE: input and output must be within .artifacts/source-inspector-offline.'
    }
    return $absolute
}
$inputPath = ArtifactPath $TrxPath
$outputPath = ArtifactPath $OutputJson
if (Test-Path -LiteralPath $outputPath) { throw 'OUTPUT_EXISTS: raw or historical evidence will not be overwritten.' }
$info = Get-Item -LiteralPath $inputPath
if ($info.PSIsContainer -or $info.Length -le 0 -or $info.Length -gt 64MB) { throw 'TRX_BYTE_LIMIT: expected a nonempty TRX no larger than 64 MiB.' }
$bytes = [IO.File]::ReadAllBytes($inputPath)
if ($bytes.Length -gt 64MB) { throw 'TRX_BYTE_LIMIT' }
$settings = [Xml.XmlReaderSettings]::new()
$settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
$settings.XmlResolver = $null
$settings.MaxCharactersInDocument = 64MB
$stream = [IO.MemoryStream]::new($bytes, $false)
$reader = [Xml.XmlReader]::Create($stream, $settings)
$document = [Xml.XmlDocument]::new()
$document.XmlResolver = $null
try { $document.Load($reader) } finally { $reader.Dispose(); $stream.Dispose() }
$ns = [Xml.XmlNamespaceManager]::new($document.NameTable)
$ns.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
$root = $document.SelectSingleNode('/t:TestRun', $ns)
if ($null -eq $root) { throw 'TRX_SCHEMA' }
$summary = $root.SelectSingleNode('t:ResultSummary', $ns)
$counters = $root.SelectSingleNode('t:ResultSummary/t:Counters', $ns)
if ($null -eq $summary -or $null -eq $counters -or $summary.GetAttribute('outcome') -ne 'Completed') { throw 'TRX_INCOMPLETE' }
function Counter([string]$name) {
    [long]$value = 0
    if (-not [long]::TryParse($counters.GetAttribute($name), [ref]$value) -or $value -lt 0) { throw "TRX_COUNTER: $name" }
    return $value
}
$total = Counter 'total'
if ($total -le 0 -or (Counter 'executed') -ne $total -or (Counter 'passed') -ne $total) { throw 'TRX_NOT_ALL_PASSED' }
foreach ($name in @('failed','error','timeout','aborted','inconclusive','passedButRunAborted','notRunnable','notExecuted','disconnected','warning','inProgress','pending')) {
    if ((Counter $name) -ne 0) { throw "TRX_UNSUCCESSFUL: $name" }
}
$results = @($root.SelectNodes('t:Results/t:UnitTestResult', $ns))
$definitions = @($root.SelectNodes('t:TestDefinitions/t:UnitTest', $ns))
if ($results.Count -ne $total -or $definitions.Count -ne $total) { throw 'TRX_CENSUS' }
$byId = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
foreach ($definition in $definitions) {
    if (-not $byId.TryAdd($definition.GetAttribute('id'), $definition)) { throw 'TRX_DUPLICATE_DEFINITION' }
}

# Frozen independent schema-1 inventory; do not infer expected populations from emitted records.
$class = 'BmsLibraryAudit.SourceInspector.Offline.Tests.ScalabilityTests'
$inventory = @(
    'charts-250000-25000-False|ProgressiveChartPopulationsHaveExactIndependentOracles(count: 250000, folderCount: 25000, shared: False)|charts|250000|486400000|WORKLOAD|EXECUTED_PASS|True',
    'charts-500000-128-True|ProgressiveChartPopulationsHaveExactIndependentOracles(count: 500000, folderCount: 128, shared: True)|charts|500000|768524288|WORKLOAD|EXECUTED_PASS|True',
    'charts-1000000-128-True|ProgressiveChartPopulationsHaveExactIndependentOracles(count: 1000000, folderCount: 128, shared: True)|charts|1000000|900000000|WORKLOAD|EXECUTED_PASS|True',
    'unique-hashes-250000|ManyUniqueHashesFitTheRevisedDefaultChartEnvelope|charts|250000|750000000|WORKLOAD|EXECUTED_PASS|True',
    'dense-pairs-447|DensePairsFitDeclaredBudgetWithoutLosingExactClosure|charts|447|80000000|WORKLOAD|EXECUTED_PASS|True',
    'chart-cancellation-150000|CancellationDuringLargeChartProcessingReturnsNoAnalysis|charts|150000|234496000|WORKLOAD|CANCELLED_EXPECTED|False',
    'shared-hash-10000|HighlySharedHashStopsBeforeQuadraticPairAllocation|charts|10000|56320000|WORKLOAD|RESOURCE_STOP_EXPECTED|False',
    'sparse-chain-20000|ManySparseRelationshipsPreserveTransitiveClosureAndExactLabels|charts|39998|143357952|WORKLOAD|EXECUTED_PASS|True',
    'four-exact-components|SeveralLargeExactComponentsKeepFullOrderedMembers|charts|800|11000000|WORKLOAD|EXECUTED_PASS|True',
    'cluster-csv-250000-valid|LargeCsvUsesGeneratedStreamWithLateMalformedRecord(malformed: False)|rows|250000|128000000|WORKLOAD|EXECUTED_PASS|True',
    'cluster-csv-250000-malformed|LargeCsvUsesGeneratedStreamWithLateMalformedRecord(malformed: True)|rows|250000|128000000|WORKLOAD|EXECUTED_PASS|False',
    'csv-cancellation-250000|CancellationDuringLargeCsvStopsWithNoCompleteFingerprint|rows|250000|16000000|WORKLOAD|CANCELLED_EXPECTED|False',
    'duplicate-csv-250000|LargeDuplicateCsvFitsDefaultStreamingEnvelopeWithEveryPhysicalRow|rows|250000|400000000|WORKLOAD|EXECUTED_PASS|True',
    'initial-million-admission|InitialMillionChartEstimateIsRejectedBeforeCalibration|charts|1000000|1638400000|ADMISSION_PLANNER|NOT_EXECUTED_RESOURCE_GATE|False',
    'accounting-control-resource|AdmissionAccountingIsVerifiedWithoutLargeAllocations(outcome: "resource")|synthetic control|1|2|CONTROL|NOT_EXECUTED_RESOURCE_GATE|False',
    'accounting-control-environment|AdmissionAccountingIsVerifiedWithoutLargeAllocations(outcome: "environment")|synthetic control|1|2|CONTROL|BLOCKED_ENVIRONMENT|False',
    'accounting-control-failed_execution|AdmissionAccountingIsVerifiedWithoutLargeAllocations(outcome: "failed_execution")|synthetic control|1|2|CONTROL|EXECUTED_FAIL|False'
)
$specs = [Collections.Generic.Dictionary[string,object]]::new([StringComparer]::Ordinal)
foreach ($line in $inventory) {
    $part = $line.Split('|')
    $specs.Add("$class.$($part[1])", [pscustomobject]@{ Id=$part[0]; Unit=$part[2]; Population=[long]$part[3]; Estimate=[long]$part[4]; Purpose=$part[5]; Outcome=$part[6]; FullData=$part[7] -eq 'True' })
}
$fields = @('Version','ScenarioId','Purpose','PopulationUnit','RequestedPopulation','EstimatedBytes','Admission','WorkloadExecuted','ExpectedResult','ActualOutcome','ActualResult','TestRunnerStatus','ElapsedMilliseconds','Measurements','Reason')
$metricNames = @('elapsed_ms','allocated_managed','sampled_peak_managed','sampled_peak_working_set','lifetime_peak_working_set')
function TextField($element, [string]$name, [bool]$nullable = $false) {
    $value = $element.GetProperty($name)
    if ($nullable -and $value.ValueKind -eq [System.Text.Json.JsonValueKind]::Null) { return }
    if ($value.ValueKind -ne [System.Text.Json.JsonValueKind]::String -or [string]::IsNullOrWhiteSpace($value.GetString()) -or $value.GetString().Length -gt 2048) { throw "RECORD_TEXT: $name" }
    return $value.GetString()
}
function IntegerField($element, [string]$name) {
    [long]$number = 0
    $value = $element.GetProperty($name)
    if ($value.ValueKind -ne [System.Text.Json.JsonValueKind]::Number -or -not $value.TryGetInt64([ref]$number) -or $number -lt 0) { throw "RECORD_INTEGER: $name" }
    return $number
}
$seenIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$seenNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$resultIds = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
$joined = [Collections.Generic.List[object]]::new()
foreach ($result in $results) {
    $name = $result.GetAttribute('testName'); $id = $result.GetAttribute('testId')
    if ($result.GetAttribute('outcome') -ne 'Passed' -or -not $resultIds.Add($id) -or -not $byId.ContainsKey($id)) { throw 'TRX_RESULT_FAILURE_OR_ID' }
    $definition = $byId[$id]
    if ($definition.GetAttribute('name') -cne $name -or $definition.SelectSingleNode('t:Execution', $ns).GetAttribute('id') -cne $result.GetAttribute('executionId')) { throw 'TRX_IDENTITY' }
    $method = $definition.SelectSingleNode('t:TestMethod', $ns)
    if ($null -eq $method) { throw 'TRX_METHOD' }
    $stdoutNode = $result.SelectSingleNode('t:Output/t:StdOut', $ns)
    $stdout = if ($null -eq $stdoutNode) { '' } else { $stdoutNode.InnerText }
    $lines = @($stdout -split '\r?\n' | Where-Object { $_.StartsWith('STRESS_EXECUTION_JSON ', [StringComparison]::Ordinal) })
    if (-not $specs.ContainsKey($name)) {
        if ($method.GetAttribute('className') -ceq $class -or $lines.Count) { throw 'UNEXPECTED_STRESS_CASE' }
        continue
    }
    $spec = $specs[$name]
    if ($method.GetAttribute('className') -cne $class -or $method.GetAttribute('name') -cne $name.Substring($class.Length + 1).Split('(')[0] -or -not $seenNames.Add($name) -or $lines.Count -ne 1) { throw 'STRESS_CASE_OR_RECORD_COUNT' }
    $raw = $lines[0].Substring('STRESS_EXECUTION_JSON '.Length)
    if ($raw.Length -gt 16384) { throw 'RECORD_BYTE_LIMIT' }
    $options = [System.Text.Json.JsonDocumentOptions]::new(); $options.MaxDepth = 8
    $json = [System.Text.Json.JsonDocument]::Parse($raw, $options)
    try {
        $element = $json.RootElement
        if ($element.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) { throw 'RECORD_OBJECT' }
        $keys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
        foreach ($property in $element.EnumerateObject()) { if (-not $keys.Add($property.Name) -or $property.Name -cnotin $fields) { throw 'RECORD_FIELDS' } }
        if ($keys.Count -ne $fields.Count) { throw 'RECORD_FIELDS' }
        $scenarioId = TextField $element 'ScenarioId'
        if ($scenarioId -cne $spec.Id -or -not $seenIds.Add($scenarioId)) { throw 'SCENARIO_IDENTITY' }
        if ((IntegerField $element 'Version') -ne 1 -or (IntegerField $element 'RequestedPopulation') -ne $spec.Population -or (IntegerField $element 'EstimatedBytes') -ne $spec.Estimate -or
            (TextField $element 'Purpose') -cne $spec.Purpose -or (TextField $element 'PopulationUnit') -cne $spec.Unit -or (TextField $element 'TestRunnerStatus') -cne 'REQUIRES_TRX_CORRELATION') { throw 'RECORD_METADATA' }
        $null = TextField $element 'ExpectedResult'
        $actual = TextField $element 'ActualResult' $true
        $reason = TextField $element 'Reason' $true
        $outcome = TextField $element 'ActualOutcome'; $admission = TextField $element 'Admission'
        if ($element.GetProperty('WorkloadExecuted').ValueKind -notin @([System.Text.Json.JsonValueKind]::True,[System.Text.Json.JsonValueKind]::False)) { throw 'RECORD_BOOLEAN' }
        $executed = $element.GetProperty('WorkloadExecuted').GetBoolean()
        $metricElement = $element.GetProperty('Measurements')
        $metrics = $null
        if ($executed) {
            if ($admission -cne 'ADMITTED' -or $outcome -cne $spec.Outcome -or $outcome -cin @('NOT_EXECUTED_RESOURCE_GATE','BLOCKED_ENVIRONMENT')) { throw 'EXECUTION_CONTRADICTION' }
            $scopeTime = IntegerField $element 'ElapsedMilliseconds'
            if ($spec.Purpose -eq 'CONTROL') {
                if ($outcome -cne 'EXECUTED_FAIL' -or -not $reason -or $metricElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Null) { throw 'CONTROL_FAILURE_RECORD' }
            } else {
                if (-not $actual -or $reason -or $metricElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Object) { throw 'EXECUTION_EVIDENCE' }
                $metrics = [ordered]@{}; $metricKeys = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
                foreach ($metric in $metricElement.EnumerateObject()) {
                    if (-not $metricKeys.Add($metric.Name) -or $metric.Name -cnotin $metricNames) { throw 'MEASUREMENT_FIELDS' }
                    $metrics[$metric.Name] = IntegerField $metricElement $metric.Name
                }
                if ($metricKeys.Count -ne 5 -or $scopeTime -lt $metrics['elapsed_ms'] -or $metrics['lifetime_peak_working_set'] -lt $metrics['sampled_peak_working_set']) { throw 'MEASUREMENT_CONTRADICTION' }
                $human = @($stdout -split '\r?\n' | Where-Object { $_ -match 'elapsed_ms=\d+; allocated_managed=\d+; sampled_peak_managed=\d+; sampled_peak_working_set=\d+; lifetime_peak_working_set=\d+$' })
                if ($human.Count -ne 1) { throw 'MEASUREMENT_SNAPSHOT_COUNT' }
                foreach ($metricName in $metricNames) {
                    if ($human[0] -notmatch "(?:^|; | )$metricName=(\d+)(?:;|$)" -or [long]$Matches[1] -ne $metrics[$metricName]) { throw 'MEASUREMENT_TEXT_JSON_MISMATCH' }
                }
                if ($scenarioId -eq 'cluster-csv-250000-malformed' -and $actual -cnotmatch 'outcome=Malformed;') { throw 'MALFORMED_ORACLE' }
            }
        } else {
            if ($stdout -match '\belapsed_ms=\d+') { throw 'NONEXECUTION_MEASUREMENT_TEXT' }
            if ($outcome -cnotin @('NOT_EXECUTED_RESOURCE_GATE','BLOCKED_ENVIRONMENT') -or -not $reason -or $actual -or $metricElement.ValueKind -ne [System.Text.Json.JsonValueKind]::Null -or $element.GetProperty('ElapsedMilliseconds').ValueKind -ne [System.Text.Json.JsonValueKind]::Null) { throw 'NONEXECUTION_CONTRADICTION' }
            if (($outcome -eq 'NOT_EXECUTED_RESOURCE_GATE' -and $admission -cne 'REJECTED_RESOURCE_GATE') -or ($outcome -eq 'BLOCKED_ENVIRONMENT' -and $admission -cnotin @('ADMITTED','BLOCKED_ENVIRONMENT'))) { throw 'ADMISSION_CONTRADICTION' }
            if ($spec.Purpose -ne 'WORKLOAD' -and $outcome -cne $spec.Outcome) { throw 'PLANNER_CONTROL_OUTCOME' }
        }
        if ($spec.Purpose -ne 'WORKLOAD' -and $spec.Outcome -ne 'EXECUTED_FAIL' -and $executed) { throw 'PLANNER_CONTROL_EXECUTION' }
        $joined.Add([pscustomobject]@{ TestName=$name; TestId=$id; TestRunnerStatus='Passed'; FullyCompletedData=$executed -and $spec.FullData -and $outcome -eq 'EXECUTED_PASS'; RawRecord=($raw | ConvertFrom-Json -AsHashtable) })
    } finally { $json.Dispose() }
}
if ($seenNames.Count -ne $specs.Count) { throw 'MISSING_SCENARIO' }
$workloads = @($joined | Where-Object { $_.RawRecord.Purpose -eq 'WORKLOAD' })
$audit = [ordered]@{
    AuditVersion=1; Accepted=$true; SourceTrx=[IO.Path]::GetFileName($inputPath)
    SourceSha256=[Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
    Framework=[ordered]@{ Total=$total; Executed=(Counter 'executed'); Passed=(Counter 'passed'); Failed=(Counter 'failed'); Skipped=(Counter 'notExecuted') }
    Accounting=[ordered]@{
        WorkloadScenarios=$workloads.Count
        WorkloadsExecuted=@($workloads | Where-Object { $_.RawRecord.WorkloadExecuted }).Count
        AdmittedIncomplete=@($workloads | Where-Object { $_.RawRecord.Admission -eq 'ADMITTED' -and -not $_.FullyCompletedData }).Count
        OmittedByAdmission=@($workloads | Where-Object { $_.RawRecord.ActualOutcome -eq 'NOT_EXECUTED_RESOURCE_GATE' }).Count
        BlockedEnvironment=@($workloads | Where-Object { $_.RawRecord.ActualOutcome -eq 'BLOCKED_ENVIRONMENT' }).Count
        PlannerCases=@($joined | Where-Object { $_.RawRecord.Purpose -eq 'ADMISSION_PLANNER' }).Count
        ControlCases=@($joined | Where-Object { $_.RawRecord.Purpose -eq 'CONTROL' }).Count
        FullyCompletedData=@($workloads | Where-Object FullyCompletedData).Count
        ExpectedCancellations=@($workloads | Where-Object { $_.RawRecord.ActualOutcome -eq 'CANCELLED_EXPECTED' }).Count
        ExpectedResourceStops=@($workloads | Where-Object { $_.RawRecord.ActualOutcome -eq 'RESOURCE_STOP_EXPECTED' }).Count
    }
    Records=@($joined)
}
$outputBytes = [Text.UTF8Encoding]::new($false).GetBytes(($audit | ConvertTo-Json -Depth 12) + [char]10)
$output = [IO.File]::Open($outputPath, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try { $output.Write($outputBytes) } finally { $output.Dispose() }
Write-Output "ACCEPTED: tests=$total; workloads=$($audit.Accounting.WorkloadsExecuted)/$($workloads.Count); completed=$($audit.Accounting.FullyCompletedData); output=$outputPath"

param(
    [string]$BaseUrl = 'http://127.0.0.1:5094'
)

$ErrorActionPreference = 'Stop'

$connectionString = 'Server=YASIN-YASIN\SQLEXPRESS;Database=LUMAR_ERP;Integrated Security=True;TrustServerCertificate=True;MultipleActiveResultSets=True'
$sqlConnection = [System.Data.SqlClient.SqlConnection]::new($connectionString)

function Invoke-JsonRequest {
    param(
        [string]$Uri,
        [string]$Method,
        [object]$Body
    )

    $json = $Body | ConvertTo-Json -Depth 8
    return Invoke-RestMethod -Uri $Uri -Method $Method -ContentType 'application/json' -Body $json
}

function Invoke-SqlNonQuery {
    param(
        [string]$Sql,
        [int]$ProductTypeId,
        [int]$ImportedReadyMadeProductId
    )

    $command = $sqlConnection.CreateCommand()
    $command.CommandText = $Sql
    $command.Parameters.AddWithValue('@productTypeId', $ProductTypeId) | Out-Null
    $command.Parameters.AddWithValue('@importedReadyMadeProductId', $ImportedReadyMadeProductId) | Out-Null
    $command.ExecuteNonQuery() | Out-Null
    $command.Dispose()
}

function Get-SqlSnapshot {
    param(
        [string]$Sql,
        [int]$ProductTypeId,
        [int]$ImportedReadyMadeProductId
    )

    $command = $sqlConnection.CreateCommand()
    $command.CommandText = $Sql
    $command.Parameters.AddWithValue('@productTypeId', $ProductTypeId) | Out-Null
    $command.Parameters.AddWithValue('@importedReadyMadeProductId', $ImportedReadyMadeProductId) | Out-Null
    $reader = $command.ExecuteReader()
    $snapshot = $null
    if ($reader.Read()) {
        $snapshot = [pscustomobject]@{
            Id = $reader.GetInt32(0)
            Points = $reader.GetDecimal(1)
            IsActive = $reader.GetBoolean(2)
        }
    }
    $reader.Close()
    $command.Dispose()
    return $snapshot
}

function Restore-Setting {
    param(
        [string]$Table,
        [string]$KeyColumn,
        [int]$Key,
        [object]$Snapshot
    )

    $command = $sqlConnection.CreateCommand()
    if ($null -eq $Snapshot) {
        $command.CommandText = "DELETE FROM dbo.$Table WHERE $KeyColumn = @key;"
    } else {
        $command.CommandText = "UPDATE dbo.$Table SET Points = @points, IsActive = @isActive, UpdatedAtUtc = SYSUTCDATETIME() WHERE $KeyColumn = @key;"
        $command.Parameters.AddWithValue('@points', $Snapshot.Points) | Out-Null
        $command.Parameters.AddWithValue('@isActive', $Snapshot.IsActive) | Out-Null
    }
    $command.Parameters.AddWithValue('@key', $Key) | Out-Null
    $command.ExecuteNonQuery() | Out-Null
    $command.Dispose()
}

$productId = $null
$importedId = $null
$program = $null
$productSettingCreated = $false
$importedSettingCreated = $false
$programChanged = $false
$productSnapshot = $null
$importedSnapshot = $null

try {
    $sqlConnection.Open()
    $products = @(Invoke-RestMethod "$BaseUrl/api/piece-points/product-settings")
    $imported = @(Invoke-RestMethod "$BaseUrl/api/piece-points/imported-product-settings")
    $product = $products[0][0]
    $importedProduct = @($imported[0] | Where-Object { $_.isProductActive })[0]
    if ($null -eq $product -or $null -eq $importedProduct) {
        throw 'لم يوجد مصدر رسمي غير مضبوط صالح للاختبار الحي.'
    }

    $productId = [int]$product.productTypeId
    $importedId = [int]$importedProduct.importedReadyMadeProductId
    $productSnapshot = Get-SqlSnapshot 'SELECT LoyaltyPiecePointSettingId, Points, IsActive FROM dbo.LoyaltyPiecePointSettings WHERE ProductTypeId = @productTypeId' $productId $importedId
    $importedSnapshot = Get-SqlSnapshot 'SELECT TOP (1) ImportedReadyMadeProductId, Points, IsActive FROM dbo.LoyaltyImportedReadyMadeProductPointSettings WHERE ImportedReadyMadeProductId = @importedReadyMadeProductId' $productId $importedId
    $program = @(Invoke-RestMethod "$BaseUrl/api/loyalty-management/program-settings")[0]
    if ($null -eq $program) { throw 'لا يوجد إعداد برنامج ولاء حي.' }

    $productUri = "$BaseUrl/api/piece-points/product-settings/$productId"
    $importedUri = "$BaseUrl/api/piece-points/imported-product-settings/$importedId"
    $evaluateUri = "$BaseUrl/api/piece-points/evaluate-official"
    $productBody = @{ productTypeId = $productId; quantity = 1; source = 'RL6-live-proof' }
    $importedBody = @{ importedReadyMadeProductId = $importedId; quantity = 1; source = 'RL6-live-proof' }

    Invoke-JsonRequest $productUri 'Put' @{ points = 7; isActive = $true } | Out-Null
    $productSettingCreated = $true
    $productActive = Invoke-JsonRequest $evaluateUri 'Post' $productBody
    Invoke-JsonRequest $productUri 'Put' @{ points = 7; isActive = $false } | Out-Null
    $productInactive = Invoke-JsonRequest $evaluateUri 'Post' $productBody
    Invoke-JsonRequest $productUri 'Put' @{ points = 7; isActive = $true } | Out-Null
    $productReenabled = Invoke-JsonRequest $evaluateUri 'Post' $productBody

    Invoke-JsonRequest $importedUri 'Put' @{ points = 9; isActive = $true } | Out-Null
    $importedSettingCreated = $true
    $importedActive = Invoke-JsonRequest $evaluateUri 'Post' $importedBody
    Invoke-JsonRequest $importedUri 'Put' @{ points = 9; isActive = $false } | Out-Null
    $importedInactive = Invoke-JsonRequest $evaluateUri 'Post' $importedBody
    Invoke-JsonRequest $importedUri 'Put' @{ points = 9; isActive = $true } | Out-Null
    $importedReenabled = Invoke-JsonRequest $evaluateUri 'Post' $importedBody

    $programUri = "$BaseUrl/api/loyalty-management/program-settings/$($program.loyaltyProgramSettingId)"
    $programPayload = @{
        isEnabled = $false
        pointsPerPiece = $program.pointsPerPiece
        pointMonetaryValue = $program.pointMonetaryValue
        effectiveFromUtc = $program.effectiveFromUtc
    }
    Invoke-JsonRequest $programUri 'Put' $programPayload | Out-Null
    $programChanged = $true
    $globallyDisabledProduct = Invoke-JsonRequest $evaluateUri 'Post' $productBody
    Invoke-JsonRequest $programUri 'Put' @{
        isEnabled = $program.isEnabled
        pointsPerPiece = $program.pointsPerPiece
        pointMonetaryValue = $program.pointMonetaryValue
        effectiveFromUtc = $program.effectiveFromUtc
    } | Out-Null
    $programChanged = $false

    $missingEvaluation = Invoke-JsonRequest $evaluateUri 'Post' @{
        productTypeId = 2147483000
        quantity = 1
        source = 'RL6-live-proof-missing'
    }

    [pscustomobject]@{
        ProductTypeId = $productId
        ProductActive = $productActive
        ProductInactive = $productInactive
        ProductReenabled = $productReenabled
        ImportedReadyMadeProductId = $importedId
        ImportedActive = $importedActive
        ImportedInactive = $importedInactive
        ImportedReenabled = $importedReenabled
        GloballyDisabledProduct = $globallyDisabledProduct
        MissingProduct = $missingEvaluation
    } | ConvertTo-Json -Depth 10
}
finally {
    if ($sqlConnection.State -ne 'Open') { $sqlConnection.Open() }
    if ($null -ne $productId) {
        Restore-Setting 'LoyaltyPiecePointSettings' 'ProductTypeId' $productId $productSnapshot
    }
    if ($null -ne $importedId) {
        Restore-Setting 'LoyaltyImportedReadyMadeProductPointSettings' 'ImportedReadyMadeProductId' $importedId $importedSnapshot
    }
    if ($programChanged) {
        $restoreUri = "$BaseUrl/api/loyalty-management/program-settings/$($program.loyaltyProgramSettingId)"
        Invoke-JsonRequest $restoreUri 'Put' @{
            isEnabled = $program.isEnabled
            pointsPerPiece = $program.pointsPerPiece
            pointMonetaryValue = $program.pointMonetaryValue
            effectiveFromUtc = $program.effectiveFromUtc
        } | Out-Null
    }
    $sqlConnection.Close()
    $sqlConnection.Dispose()
}
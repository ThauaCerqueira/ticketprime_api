$apiBase = "http://localhost:5164"

function Invoke-Api($uri, $method, $body, $headers) {
    $params = @{ Uri = $uri; Method = $method; ContentType = 'application/json' }
    if ($body) { $params['Body'] = $body }
    if ($headers) { $params['Headers'] = $headers }
    try {
        return Invoke-RestMethod @params
    } catch {
        $msg = $_.Exception.Message
        $detail = ""
        if ($_.Exception.Response) {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $reader.BaseStream.Position = 0
            $reader.DiscardBufferedData()
            $detail = $reader.ReadToEnd()
        }
        Write-Host "X Error: $msg" -ForegroundColor Red
        if ($detail) { Write-Host "  Details: $detail" -ForegroundColor Red }
        throw
    }
}

# Use existing user CPF 66301020022 (already registered and email verified)
$cpf = "66301020022"

Write-Host "=== Step 1: Logging in as client (CPF: $cpf) ===" -ForegroundColor Cyan
$loginBody = "{`"cpf`":`"$cpf`",`"senha`":`"Test@1234`"}"
try {
    $loginResult = Invoke-Api -uri "$apiBase/api/auth/login" -method Post -body $loginBody
    $clientToken = $loginResult.token
    Write-Host "- Client logged in successfully!" -ForegroundColor Green
} catch {
    Write-Host "X Login failed" -ForegroundColor Red
    exit 1
}

Write-Host "=== Step 2: Getting event details ===" -ForegroundColor Cyan
try {
    $eventDetail = Invoke-Api -uri "$apiBase/api/eventos/2" -method Get
    Write-Host "- Event found: $($eventDetail.nome)" -ForegroundColor Green
    Write-Host "  Date: $($eventDetail.dataEvento), Local: $($eventDetail.local)" -ForegroundColor Gray
    Write-Host "  Ticket Types:" -ForegroundColor Yellow
    foreach ($tt in $eventDetail.tiposIngresso) {
        Write-Host "    - [$($tt.id)] $($tt.nome): R$ $($tt.preco) ($($tt.capacidadeRestante) available)" -ForegroundColor Gray
    }
} catch {
    Write-Host "X Failed to get event details" -ForegroundColor Red
    exit 1
}

Write-Host "=== Step 3: Buying 2 tickets (Pista - R$ 150 each, via PIX) ===" -ForegroundColor Cyan
$headers = @{ Authorization = "Bearer $clientToken" }

for ($i = 1; $i -le 2; $i++) {
    Write-Host "--- Ticket $i of 2 ---" -ForegroundColor Magenta
    
    $purchaseBody = @{
        eventoId = 2
        ticketTypeId = 1
        metodoPagamento = "pix"
        contratarSeguro = $false
        ehMeiaEntrada = $false
    }
    $purchaseJson = $purchaseBody | ConvertTo-Json
    
    try {
        $purchaseResult = Invoke-Api -uri "$apiBase/api/reservas" -method Post -body $purchaseJson -headers $headers
        Write-Host "- Ticket $i purchased!" -ForegroundColor Green
        Write-Host "  Reservation ID: $($purchaseResult.reservaId)" -ForegroundColor Gray
        Write-Host "  Ticket Code: $($purchaseResult.codigoIngresso)" -ForegroundColor Gray
        Write-Host "  Amount Paid: R$ $($purchaseResult.valorPago)" -ForegroundColor Gray
    } catch {
        Write-Host "X Purchase failed for ticket $i" -ForegroundColor Red
    }
    
    # Wait 25 seconds between purchases to respect 3/min rate limit
    if ($i -lt 2) {
        Write-Host "  Waiting 25s before next purchase (rate limit: 3/min)..." -ForegroundColor Yellow
        Start-Sleep -Seconds 25
    }
}

Write-Host "=== Step 4: Listing my tickets ===" -ForegroundColor Cyan
try {
    $myReservas = Invoke-Api -uri "$apiBase/api/reservas" -method Get -headers $headers
    $count = $myReservas.Count
    Write-Host "- Found $count ticket(s):" -ForegroundColor Green
    foreach ($r in $myReservas) {
        Write-Host "  - [$($r.codigoIngresso)] $($r.nomeEvento) - R$ $($r.valorFinalPago) - Status: $($r.status)" -ForegroundColor Gray
        Write-Host "    Evento: $($r.nomeEvento), Data: $($r.dataEvento), Local: $($r.local)" -ForegroundColor DarkGray
    }
} catch {
    Write-Host "X Failed to list tickets: $($_.Exception.Message)" -ForegroundColor Red
}

Write-Host "=== Script completed! ===" -ForegroundColor Cyan
Write-Host "Client CPF: $cpf" -ForegroundColor Yellow

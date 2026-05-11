$token = (Invoke-RestMethod -Uri 'http://localhost:5164/api/auth/login' -Method Post -Body '{"cpf":"00000000191","senha":"admin123"}' -ContentType 'application/json').token

$body = Get-Content -Path 'evento.json' -Raw -Encoding UTF8

Write-Host "Sending event creation request..."
try {
    $response = Invoke-RestMethod -Uri 'http://localhost:5164/api/eventos' -Method Post -Body $body -ContentType 'application/json; charset=utf-8' -Headers @{Authorization = "Bearer $token"}
    Write-Host "SUCCESS! Event created:"
    $response | ConvertTo-Json -Depth 10
}
catch {
    Write-Host "ERROR: $($_.Exception.Message)"
    if ($_.Exception.Response) {
        $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
        $reader.BaseStream.Position = 0
        $reader.DiscardBufferedData()
        $errorBody = $reader.ReadToEnd()
        Write-Host "Response body: $errorBody"
    }
}

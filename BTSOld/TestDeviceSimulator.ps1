
$listenPort = 10002

try {

    $udpClient = New-Object System.Net.Sockets.UdpClient($listenPort)
    $udpClient.EnableBroadcast = $true

    Write-Host "Listening on UDP port $listenPort..."
    Write-Host "Press Ctrl+C to stop."

    while ($true) {

        try {

            $remoteEndPoint = New-Object System.Net.IPEndPoint([System.Net.IPAddress]::Any,0)

            $bytes = $udpClient.Receive([ref]$remoteEndPoint)

            $hex = ($bytes | ForEach-Object { $_.ToString("X2") }) -join " "

            Write-Host ""
            Write-Host "Received from $($remoteEndPoint.Address):$($remoteEndPoint.Port)"
            Write-Host $hex

        }
        catch {
            Write-Host "Error: $($_.Exception.Message)" -ForegroundColor Red
        }
    }
}
finally {

    if ($udpClient) {
        $udpClient.Close()
    }

    Write-Host "Listener stopped."
}
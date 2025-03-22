New-Service -Name "Transcription Service" -BinaryPathName "E:\s\repos\TranscriptionService\app\TranscriptionServiceHost.exe"
Start-Service -Name "Transcription Service"
Get-Service -Name "Transcription Service"
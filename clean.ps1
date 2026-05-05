Get-ChildItem -Path . -Filter "bin" -Directory -Recurse | ForEach-Object { 
    Remove-Item $_.FullName -Recurse -Force
}

Get-ChildItem -Path . -Filter "obj" -Directory -Recurse | ForEach-Object { 
    Remove-Item $_.FullName -Recurse -Force
}
Get-ChildItem -Path . -Filter "bin" -Directory -Recurse |
    Where-Object { $_.FullName -notlike "*\node_modules\*" } |
    ForEach-Object { Remove-Item $_.FullName -Recurse -Force }

Get-ChildItem -Path . -Filter "obj" -Directory -Recurse |
    Where-Object { $_.FullName -notlike "*\node_modules\*" } |
    ForEach-Object { Remove-Item $_.FullName -Recurse -Force }
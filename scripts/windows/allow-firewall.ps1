# LeadersBoards: let your phone reach the API through Windows Firewall.
# start.bat runs this for you (Windows asks "Do you want to allow this app to make changes?": click Yes).
# It needs admin rights because firewall rules affect the whole PC. It does two things:
#   1. Removes any "Block" rules for the API program. Windows creates these silently if you ever
#      clicked Cancel on the firewall popup, and a Block rule beats any Allow rule.
#   2. Adds one rule allowing incoming connections on port 5271 (the API) on Private networks only,
#      i.e. your home Wi-Fi, not public Wi-Fi in a cafe.

$ruleName = 'LeadersBoards API (dev, port 5271)'

Get-NetFirewallRule -Direction Inbound -Action Block -ErrorAction SilentlyContinue |
    Where-Object { ($_ | Get-NetFirewallApplicationFilter).Program -match 'LeadersBoards\.Api|\\dotnet\.exe$' } |
    ForEach-Object { Write-Host "Removing block rule: $($_.DisplayName)"; Remove-NetFirewallRule -Name $_.Name }

if (-not (Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Protocol TCP -LocalPort 5271 `
        -Action Allow -Profile Private, Domain | Out-Null
}
Write-Host "Firewall rule '$ruleName' is in place."

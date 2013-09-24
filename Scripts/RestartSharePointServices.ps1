param
(
    [Parameter(Mandatory=$false, HelpMessage='-ServiceNames Optional, provide a set of service names to restart.')]
    [Array]$ServiceNames=@("SharePoint 2010 Timer","SharePoint 2010 Administration","IIS Admin Service","World Wide Web Publishing Service","Web Analytics Service")
);
 
Write-Host "Attempting to get SharePoint Servers in Farm" -ForegroundColor White;
$farm = Get-SPFarm;
$servers = $farm.Servers;
Write-Host "Found" $servers.Count "Servers in Farm (including database servers)" -ForegroundColor White;
foreach($server in $servers)
{
    if($server.Role -ne [Microsoft.SharePoint.Administration.SPServerRole]::Invalid)
    {
        Write-Host "Attempting to restart services on" $server.Name -ForegroundColor White;
        foreach($serviceName in $ServiceNames)
        {
            $serviceInstance = Get-Service -ComputerName $server.Name -Name $serviceName -ErrorAction SilentlyContinue;
            if($serviceInstance -ne $null)
            {
                Write-Host "Attempting to restart service" $serviceName ".." -ForegroundColor White -NoNewline;
                try
                {
                    $restartServiceOutput="";
                    Restart-Service -InputObject $serviceInstance;
                    Write-Host " Done!" -ForegroundColor Green;
                }
                catch
                {
                    Write-Host "Error Occured: " $_.Message;
                }
            }
        }
    }
}
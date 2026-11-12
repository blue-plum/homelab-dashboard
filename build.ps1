$ErrorActionPreference = 'Stop'
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:winexe /out:"$PSScriptRoot\HomelabDashboard-v8.6.exe" "/resource:$PSScriptRoot\lookups.json,lookups.json" "/resource:$PSScriptRoot\Images\homelab-title.png,homelab-title.png" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Web.Extensions.dll "$PSScriptRoot\HomelabDashboard.cs" "$PSScriptRoot\Topology.cs" "$PSScriptRoot\MapRouting.cs" "$PSScriptRoot\HlbFile.cs" "$PSScriptRoot\NetworkSettings.cs" "$PSScriptRoot\ArpDiscovery.cs" "$PSScriptRoot\PortChecks.cs"
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }



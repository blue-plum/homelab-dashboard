# Homelab Dashboard
A Windows desktop application for managing network devices, checking connectivity and drawing a network layout with editable cables and labelled ports.

## Build and run
On Windows with .NET Framework 4.x installed, run `./build.ps1` in PowerShell, then open `HomelabDashboard-v8.6.exe`. No extra packages are required.

## Network files
The dashboard starts empty. Choose **Menu > Add device** to start a network, or **Menu > Load** to open an existing .hlb file. Use **Menu > Save** to keep devices, layout changes, images and attachments together. Keep personal network files outside the repository.

The distribution uses generic lookup defaults. Importing a legacy inventory preserves its individual records without household-specific automatic merges.

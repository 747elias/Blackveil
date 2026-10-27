$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location (Split-Path $PSScriptRoot -Parent)
Add-Type -AssemblyName System.Drawing
Add-Type @'
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
public static class BlackveilSetupUi {
 public delegate bool EnumProc(IntPtr hwnd, IntPtr data);
 [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc cb, IntPtr data);
 [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr hwnd, EnumProc cb, IntPtr data);
 [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hwnd, StringBuilder text, int count);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
 [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hwnd, uint message, IntPtr wp, IntPtr lp);
 [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hwnd, uint message, IntPtr wp, IntPtr lp);
 [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left,Top,Right,Bottom; }
 public static string Text(IntPtr hwnd) { var b=new StringBuilder(512); GetWindowText(hwnd,b,512);return b.ToString(); }
 public static IntPtr Find() {
  IntPtr found=IntPtr.Zero;
  EnumWindows((hwnd,data)=> { uint pid;GetWindowThreadProcessId(hwnd,out pid);
   try { if(IsWindowVisible(hwnd) && Process.GetProcessById((int)pid).ProcessName.StartsWith("Blackveil-Setup",StringComparison.OrdinalIgnoreCase)) { found=hwnd; return false; } } catch {}
   return true;
  },IntPtr.Zero);return found;
 }
 public static void ConfirmClose(IntPtr hwnd) {
  EnumChildWindows(hwnd,(child,data)=>{string text=Text(child).Replace("&","");if(text=="Ja"||text=="Yes")SendMessage(child,0xF5,IntPtr.Zero,IntPtr.Zero);return true;},IntPtr.Zero);
 }
}
'@
function Wait-Window {
    $clock = [Diagnostics.Stopwatch]::StartNew()
    do {
        $window = [BlackveilSetupUi]::Find()
        if ($window -ne [IntPtr]::Zero) { return $window }
        Start-Sleep -Milliseconds 100
    } while ($clock.Elapsed.TotalSeconds -lt 20)
    throw 'Setup UI did not appear.'
}
function Capture-Window([IntPtr]$Window, [string]$Name) {
    Start-Sleep -Milliseconds 400
    $rect = New-Object BlackveilSetupUi+Rect
    [void][BlackveilSetupUi]::GetWindowRect($Window,[ref]$rect)
    $bitmap = New-Object Drawing.Bitmap(($rect.Right-$rect.Left),($rect.Bottom-$rect.Top))
    $graphics = [Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left,$rect.Top,0,0,$bitmap.Size)
        $bitmap.Save((Join-Path (Get-Location).Path "artifacts/screenshots/$Name.png"),[Drawing.Imaging.ImageFormat]::Png)
    } finally { $graphics.Dispose();$bitmap.Dispose() }
}
function Close-Setup {
    $window = [BlackveilSetupUi]::Find()
    if ($window -ne [IntPtr]::Zero) { [void][BlackveilSetupUi]::PostMessage($window,0x10,[IntPtr]::Zero,[IntPtr]::Zero) }
    $clock = [Diagnostics.Stopwatch]::StartNew()
    do {
        Start-Sleep -Milliseconds 100
        $dialog = [BlackveilSetupUi]::Find()
        if ($dialog -eq [IntPtr]::Zero) { return }
        [BlackveilSetupUi]::ConfirmClose($dialog)
    } while ($clock.Elapsed.TotalSeconds -lt 10)
    throw 'Setup did not close.'
}
try {
    New-Item artifacts/screenshots -ItemType Directory -Force | Out-Null
    [xml]$props = Get-Content Directory.Build.props
    $setup = (Resolve-Path "artifacts/release/Blackveil-Setup-$($props.Project.PropertyGroup.Version).exe").Path
    $scope = Start-Process $setup -PassThru
    Capture-Window (Wait-Window) 'setup-scope'
    Close-Setup
    [void]$scope.WaitForExit(10000)
    $wizard = Start-Process $setup -ArgumentList '/CURRENTUSER' -PassThru
    $window = Wait-Window
    Capture-Window $window 'setup-welcome'
    Close-Setup
    [void]$wizard.WaitForExit(10000)
    Write-Host 'PASS: scope chooser and branded setup welcome screen displayed; screenshots captured without installing.'
} finally {
    Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -like 'Blackveil-Setup-*' } | Stop-Process -Force
    Pop-Location
}

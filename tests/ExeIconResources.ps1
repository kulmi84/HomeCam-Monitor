param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$file = (Resolve-Path -LiteralPath $Executable).Path
Add-Type -AssemblyName System.Drawing
if (-not ('HomeCamIconCheck.Native' -as [type])) {
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace HomeCamIconCheck {
 public static class Native {
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)] public static extern uint ExtractIconEx(string file,int index,IntPtr[] large,IntPtr[] small,uint count);
  [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] public static extern IntPtr LoadLibraryEx(string file,IntPtr reserved,uint flags);
  [DllImport("kernel32.dll")] public static extern bool FreeLibrary(IntPtr module);
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindResource(IntPtr module,IntPtr name,IntPtr type);
  [DllImport("kernel32.dll")] static extern uint SizeofResource(IntPtr module,IntPtr resource);
  [DllImport("kernel32.dll")] static extern IntPtr LoadResource(IntPtr module,IntPtr resource);
  [DllImport("kernel32.dll")] static extern IntPtr LockResource(IntPtr resource);
  public static byte[] Read(IntPtr module,int type,int id) {
   var resource=FindResource(module,(IntPtr)id,(IntPtr)type);
   if(resource==IntPtr.Zero) throw new Exception("Missing native resource: "+type+"/"+id);
   var bytes=new byte[SizeofResource(module,resource)];
   Marshal.Copy(LockResource(LoadResource(module,resource)),bytes,0,bytes.Length);return bytes;
  }
 }
}
'@
}
if ([HomeCamIconCheck.Native]::ExtractIconEx($file,-1,$null,$null,0) -ne 3) { throw 'Windows does not enumerate exactly three EXE icons.' }
$module = [HomeCamIconCheck.Native]::LoadLibraryEx($file,[IntPtr]::Zero,0x22)
if ($module -eq [IntPtr]::Zero) { throw 'Cannot inspect EXE resources.' }
try {
    $sources = @('Default','White','Black')
    for ($variant=0; $variant -lt 3; $variant++) {
        $ico = [IO.File]::ReadAllBytes((Join-Path $root "assets/homecam-icon-$($sources[$variant]).ico"))
        $group = [HomeCamIconCheck.Native]::Read($module,14,32512+$variant)
        if ([BitConverter]::ToUInt16($group,4) -ne 7) { throw 'EXE variant lost one of its seven icon sizes.' }
        for ($frame=0; $frame -lt 7; $frame++) {
            $entry=6+$frame*16; $size=[BitConverter]::ToUInt32($ico,$entry+8); $offset=[BitConverter]::ToUInt32($ico,$entry+12)
            $id=[BitConverter]::ToUInt16($group,6+$frame*14+12)
            $actual=[HomeCamIconCheck.Native]::Read($module,3,$id)
            $sha=[Security.Cryptography.SHA256]::Create()
            try {
                if ([Convert]::ToHexString($sha.ComputeHash($actual)) -ne [Convert]::ToHexString($sha.ComputeHash($ico,$offset,$size))) { throw "Changed icon image: $variant/$frame" }
            } finally { $sha.Dispose() }
        }
        $large=[IntPtr[]]::new(1); $small=[IntPtr[]]::new(1)
        try {
            if ([HomeCamIconCheck.Native]::ExtractIconEx($file,$variant,$large,$small,1) -lt 1 -or $large[0] -eq [IntPtr]::Zero -or $small[0] -eq [IntPtr]::Zero) { throw "Windows cannot extract variant $variant." }
        } finally {
            if($large[0] -ne [IntPtr]::Zero){[void][HomeCamIconCheck.Native]::DestroyIcon($large[0])}
            if($small[0] -ne [IntPtr]::Zero){[void][HomeCamIconCheck.Native]::DestroyIcon($small[0])}
        }
    }
    # Updating native resources must preserve the application manifest and version.
    [void][HomeCamIconCheck.Native]::Read($module,24,1)
    $version = & (Join-Path $root '.github/scripts/get-v2-version.ps1')
    $info = [Diagnostics.FileVersionInfo]::GetVersionInfo($file)
    if($info.FileVersion -ne $version -or $info.ProductVersion -ne $version) { throw 'EXE version information was changed or lost.' }
} finally { [void][HomeCamIconCheck.Native]::FreeLibrary($module) }
Write-Output 'PASS: Windows enumerates/extracts three EXE icons; all 21 source images, application manifest and version information retained.'

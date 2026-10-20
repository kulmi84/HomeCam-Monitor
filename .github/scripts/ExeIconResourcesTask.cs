using System;
using System.IO;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace HomeCamBuild {
 public static class IconResources {
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern IntPtr BeginUpdateResource(string file, bool deleteExisting);
  [DllImport("kernel32.dll", SetLastError=true)] static extern bool UpdateResource(IntPtr update, IntPtr type, IntPtr name, ushort language, byte[] data, uint size);
  [DllImport("kernel32.dll", SetLastError=true)] static extern bool EndUpdateResource(IntPtr update, bool discard);
  [DllImport("shell32.dll", CharSet=CharSet.Unicode)] public static extern uint ExtractIconEx(string file, int index, IntPtr[] large, IntPtr[] small, uint count);
  [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
  static void Put(IntPtr update, int type, int id, byte[] bytes) {
   if (!UpdateResource(update,(IntPtr)type,(IntPtr)id,0,bytes,(uint)bytes.Length)) throw new Win32Exception(Marshal.GetLastWin32Error());
  }
  static void AddGroup(IntPtr update, string file, int group, int firstImage) {
   var ico=File.ReadAllBytes(file);
   if (ico.Length<6 || BitConverter.ToUInt16(ico,0)!=0 || BitConverter.ToUInt16(ico,2)!=1) throw new InvalidDataException("Invalid ICO: "+file);
   int count=BitConverter.ToUInt16(ico,4);
   if(count<1 || count>32 || ico.Length<6+count*16) throw new InvalidDataException("Invalid ICO directory.");
   using(var memory=new MemoryStream()) using(var writer=new BinaryWriter(memory)) {
    writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)count);
    for(int i=0;i<count;i++) {
     int entry=6+i*16; uint size=BitConverter.ToUInt32(ico,entry+8),offset=BitConverter.ToUInt32(ico,entry+12);
     if((ulong)offset+size>(ulong)ico.Length || size>int.MaxValue) throw new InvalidDataException("Invalid ICO image.");
     var image=new byte[(int)size];Buffer.BlockCopy(ico,(int)offset,image,0,image.Length);
     Put(update,3,firstImage+i,image);
     writer.Write(ico,entry,12);writer.Write((ushort)(firstImage+i));
    }
    Put(update,14,group,memory.ToArray());
   }
  }
  public static void Add(string executable,string white,string black) {
   // Retain the compiler's first/default icon, version info and manifest.
   // Stable group/image IDs make repeated and incremental builds idempotent.
   var update=BeginUpdateResource(executable,false);
   if(update==IntPtr.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());
   bool commit=false;
   try {AddGroup(update,white,32513,1001);AddGroup(update,black,32514,1101);commit=true;}
   finally {if(!EndUpdateResource(update,!commit) && commit) throw new Win32Exception(Marshal.GetLastWin32Error());}
   if(ExtractIconEx(executable,-1,null,null,0)!=3) throw new InvalidDataException("Windows must see exactly three icon groups in the EXE.");
  }
 }
}
namespace HomeCamBuild {
 public sealed class ExeIconResourcesTask : Microsoft.Build.Utilities.Task {
  [Microsoft.Build.Framework.Required] public string Executable {get;set;}
  [Microsoft.Build.Framework.Required] public string WhiteIcon {get;set;}
  [Microsoft.Build.Framework.Required] public string BlackIcon {get;set;}
  public override bool Execute() {
   try { IconResources.Add(Executable,WhiteIcon,BlackIcon); Log.LogMessage(Microsoft.Build.Framework.MessageImportance.High,"Embedded three Windows-selectable EXE icon groups (standard, white, black)."); return true; }
   catch(System.Exception error) { Log.LogErrorFromException(error);return false; }
  }
 }
}
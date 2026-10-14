using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using SeaChestGUI;
class CoreTests {
 static int n=0;
 static void Check(bool ok,string label){if(!ok)throw new Exception(label);n++;}
 static void Reject(Action a,string label){bool failed=false;try{a();}catch(ArgumentException){failed=true;}Check(failed,label);}
 static void Main(string[] argv){
  string help="Usage\n\topenSeaChest_Basics -d PD0 --erase\nUtility Options\n\t-h, --help\n\t\tShow help.\n\t-v [0-4], --verbose [0 | 1 | 2]\n\t\tVerbosity\n\t--progress [dst]\n\t\tUse --progress dst.\n\t--scan\n\t\tScan all devices\n\t-S, --Scan\n\t\tLow level scan\n\t-d, --device [handle | all]\n\t\tDevice handle\n";
  var options=Protocol.ParseHelp(help);Check(options.Count==6,"No description or example becomes an option");
  Check(options.Any(o=>o.Flag=="--verbose"&&o.TakesValue),"Alias prefers long spelling");
  Check(options.Any(o=>o.Flag=="--scan")&&options.Any(o=>o.Flag=="--Scan"),"Case-sensitive scan flags preserved");
  Check(Protocol.ParseHelp("    --foo [file]\n        Description\n    --bar\n        Another description").Count==2,"Spaces supported");
  var disks=Protocol.ParseScan("Handle Model Serial Firmware\nPD0  MODEL A  SERIAL0  FW0\nPD1 MODEL-B SERIAL1 FW1\nPD0 duplicate\n\\\\.\\PhysicalDrive2  MODEL C SERIAL2 FW2\nCSMI:0:1:2 MODEL-D SERIAL3 FW3\n--scan PD9 example");
  Check(disks.Count==4,"Only scan rows with device handles accepted");Check(disks[2].Handle=="PD2","PhysicalDrive normalization");
  Check(disks[0].Identity=="PD0   MODEL A SERIAL0 FW0","Stable identity whitespace");
  // Windows v26.09-alpha-zbd layout reported by the user; serials are synthetic.
  string windowsScan="Vendor   Handle       Model Number            Serial Number          FwRev\r\n"
    +"NVMe     PD0          CT4000P3PSSD8           TEST-SERIAL-0          P9CR40A\r\n"
    +"SATA     PD1          WDC WD40EZRZ-22GXCB0    TEST-SERIAL-1          80.00A80\r\n"
    +"smi      PD2          SM2320XT USB            TEST-SERIAL-2          1000\r\n"
    +"ST12000N PD3          M0127                   TEST-SERIAL-3          0\r\n";
  var windows=Protocol.ParseScan(windowsScan);
  Check(windows.Count==4,"Windows Vendor-first table yields all four drives");
  Check(windows.Select(d=>d.Handle).SequenceEqual(new[]{"PD0","PD1","PD2","PD3"}),"Windows handles remain exact");
  Check(windows[1].Identity=="PD1   WDC WD40EZRZ-22GXCB0 TEST-SERIAL-1 80.00A80","Model with spaces and serial retained");
  Check(windows[2].Identity.Contains("SM2320XT USB"),"USB model retained");
  Check(windows[3].Identity=="PD3   M0127 TEST-SERIAL-3 0","Vendor text not mistaken for handle");
  Check(Protocol.ParseScan(windowsScan.Replace("   ","\t"))[1].Identity==windows[1].Identity,"Stable identity for rescans with different spacing");
  Check(Protocol.ParseScan("NVMe PD0 MODEL SERIAL FW").Count==0,"Vendor prefix requires an actual header");
  Check(Protocol.ParseScan(windowsScan+"Use PD9 as an example in documentation\n--scan PD8 example").Count==4,"Do not capture example prose after table");
  Check(Protocol.ParseScan(windowsScan+"Handle Model Serial Firmware\nPD4 MODEL FOUR SERIAL4 FW4\nExample PD5 text").Count==5,"Switching table format resets vendor handling");
  var built=Protocol.Build("PD1",new[]{new Argument("--downloadFW",@"C:\Firmware con espacios\archivo.bin"),new Argument("--downloadMode","deferred")});
  Check(built.SequenceEqual(new[]{"-d","PD1","--downloadFW",@"C:\Firmware con espacios\archivo.bin","--downloadMode","deferred"}),"Correct discrete arguments");
  Reject(()=>Protocol.Build("all",new[]{new Argument("--erase")}),"No all target");
  Reject(()=>Protocol.Build("PD1",new[]{new Argument("--device","PD0")}),"No duplicate target");
  Reject(()=>Protocol.Build("PD1",new[]{new Argument("--data","--device=PD0")}),"No option injection through values");
  Reject(()=>Protocol.Build(null,new[]{new Argument("--data","a\nb")}),"No newline argument");
  Reject(()=>Protocol.Build(null,new[]{new Argument("--device=all")}),"No inline target");
  Check(Protocol.Quote(@"C:\path with spaces\")=="\"C:\\path with spaces\\\\\"","Trailing backslash quoted");
  Check(Protocol.Quote("a\"b")=="\"a\\\"b\"","Embedded quote escaped");
  Check(Protocol.Quote("")=="\"\"","Empty argv item");
  Check(Protocol.Build(null,new[]{new Argument("--value","-1")}).Last()=="-1","Negative numeric argument");
  Check(Protocol.ExitText(9).Contains("administrador"),"Permissions error mapped");
  if(argv.Length>0){
   int total=0,modules=0;
   foreach(var p in Directory.GetFiles(argv[0],"*.txt")) {
    string source=File.ReadAllText(p);var list=Protocol.ParseHelp(source);
    int headers=source.Split('\n').Where(l=>Regex.IsMatch(l,@"^\t-{1,2}[A-Za-z]")).Select(l=> {
      var longFlag=Regex.Match(l,@"--[A-Za-z][A-Za-z0-9_-]*");
      return longFlag.Success?longFlag.Value:Regex.Match(l,@"-[A-Za-z][A-Za-z0-9_-]*").Value;
    }).Distinct().Count();
    Check(list.Count==headers,"Every real help header parsed: "+Path.GetFileName(p));
    Check(list.Any(o=>o.Flag=="--device")&&list.Any(o=>o.Flag=="--help"),"Common flags found: "+p);
    total+=list.Count;modules++;
   }
   Console.WriteLine("Official Linux help fixtures: "+modules+" modules, "+total+" option entries.");
  }
  if(argv.Length>1){
   string path=Path.GetFullPath(argv[1]);var tool=new Tool{Path=path,Name="fake",Hash=Tool.Digest(path)};
   var values=new List<string>{"simple","with space",@"C:\Folder with spaces\", "quote\"inside","José", "& echo nope", ""};
   var r=Runner.Run(tool,values,Directory.GetCurrentDirectory(),null,10000).GetAwaiter().GetResult();
   string expected=string.Join("\n",values.Select(s=>Convert.ToBase64String(Encoding.UTF8.GetBytes(s))));
   Check(r.Code==0&&r.Output.TrimEnd('\r','\n')==expected.TrimEnd('\r','\n'),"Runner transports exact values without shell interpretation");
   r=Runner.Run(tool,new List<string>{"exit3"},Directory.GetCurrentDirectory(),null,10000).GetAwaiter().GetResult();
   Check(r.Code==3&&r.Output.Contains("FAKE STDERR"),"stderr and nonzero exit captured");
   bool timed=false;try{Runner.Run(tool,new List<string>{"wait"},Directory.GetCurrentDirectory(),null,100).GetAwaiter().GetResult();}catch(TimeoutException){timed=true;}Check(timed,"Metadata query timeout");
   tool.Hash="invalid";bool changed=false;try{Runner.Run(tool,values,Directory.GetCurrentDirectory(),null,1000).GetAwaiter().GetResult();}catch(IOException){changed=true;}Check(changed,"Changed binary rejected before launch");
  }
  Console.WriteLine("PASS: "+n+" checks");
 }
}

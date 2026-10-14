using System;using System.Linq;using SeaChestGUI;
class ResultsTests {
 static void Main() {
  string raw="Scan\nVendor   Handle   Model Number     Serial Number   FwRev\n-------------------------------------------------------\nSATA     PD1      MODEL WITH SPACE SERIAL-1        1.0\nUSB      PD2      USB MODEL        SERIAL-2        2.0\nDone\n";
  var disks=Protocol.ParseScan(raw);disks[0].Details=new DiskDetails{SizeBytes=8000000000000UL,LettersKnown=true,Letters=new[]{"D:","E:"}};disks[1].Details=new DiskDetails{SizeBytes=500000000000UL,LettersKnown=true};
  string result=Protocol.FormatScanResults(raw,disks);var lines=result.Split('\n');
  if(!(lines[1].IndexOf("Handle")<lines[1].IndexOf("Capacidad")&&lines[1].IndexOf("Capacidad")<lines[1].IndexOf("Unidad")&&lines[1].IndexOf("Unidad")<lines[1].IndexOf("Model Number")))throw new Exception("Column order");
  if(lines[3].IndexOf("8 TB")!=lines[1].IndexOf("Capacidad")||lines[4].IndexOf("N/A")!=lines[1].IndexOf("Unidad"))throw new Exception("Alignment and N/A");
  if(!result.Contains("D:, E:"))throw new Exception("Multiple letters");
  if(!result.Contains("MODEL WITH SPACE SERIAL-1        1.0")||!result.EndsWith("Done\n"))throw new Exception("Original information preserved");
  if(Protocol.ParseScan(raw)[0].Identity!=disks[0].Identity)throw new Exception("Raw identity changed");
  string legacy="Handle          Model Number\nPD1             MODEL\n";
  if(!Protocol.FormatScanResults(legacy,disks).Contains("8 TB"))throw new Exception("Handle-first layout");
  Console.WriteLine("PASS: 6 results table checks\n"+result);
 }
}

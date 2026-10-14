using System;using System.Collections.Generic;using System.Linq;using SeaChestGUI;
class MetadataTests {
 static int count;
 static void Check(bool value,string label){if(!value)throw new Exception(label);count++;}
 static void Main(){
  Check(new DiskDetails{SizeBytes=4000000000000UL}.CapacityLabel=="4 TB","Physical capacity in decimal TB");
  Check(new DiskDetails{SizeBytes=500000000000UL}.CapacityLabel=="500 GB","GB size");
  Check(new DiskDetails{SizeBytes=1500000000000UL}.CapacityLabel=="1,5 TB","Spanish fractional capacity");
  Check(new DiskDetails().CapacityLabel=="Capacidad n/d","Unavailable capacity");
  Check(new DiskDetails{SizeBytes=0}.CapacityLabel=="Capacidad n/d","No false zero-size disk");
  Check(new DiskDetails().LettersLabel=="Letras n/d","Unknown distinct from no letter");
  Check(new DiskDetails{LettersKnown=true}.LettersLabel=="N/A","Unassigned volume");
  string partition="Disk #0, Partition #1";
  Check(WindowsDiskMetadata.DeviceIdFromPath(@"\\PC\root\cimv2:Win32_DiskPartition.DeviceID=""Disk #0, Partition #1""")==partition,"Full WMI reference path");
  Check(WindowsDiskMetadata.DeviceIdFromPath("Win32_LogicalDisk.DeviceID=\"C:\"")=="C:","Logical letter path");
  Check(WindowsDiskMetadata.DeviceIdFromPath("malformed")==null,"Malformed reference not guessed");
  var disks=new Dictionary<string,DiskDetails>(StringComparer.OrdinalIgnoreCase){
   {"PD0",new DiskDetails{SizeBytes=4000000000000UL}},
   {"PD1",new DiskDetails{SizeBytes=12000000000000UL}},
   {"PD2",new DiskDetails{SizeBytes=64000000000UL}}
  };
  var parts=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase){
   {partition,"PD0"},{"Disk #0, Partition #2","PD0"},{"Disk #1, Partition #0","PD1"}
  };
  var links=new[]{
   new KeyValuePair<string,string>("Disk #1, Partition #0","F:"),
   new KeyValuePair<string,string>("Disk #0, Partition #2","d:"),
   new KeyValuePair<string,string>(partition,"C:"),
   new KeyValuePair<string,string>(partition,"c:"),
   new KeyValuePair<string,string>("Unknown","Z:"),
   new KeyValuePair<string,string>(partition,"\\\\server\\share")
  };
  WindowsDiskMetadata.ApplyLetters(disks,parts,links);
  Check(disks["PD0"].LettersLabel=="C:, D:","All partitions mapped, sorted, deduplicated");
  Check(disks["PD1"].LettersLabel=="F:","Letters not mixed across physical disks");
  Check(disks["PD2"].LettersLabel=="N/A","Unpartitioned drive retained");
  var d=new Disk{Handle="PD0",Identity="PD0   SSD MODEL SERIAL FIRMWARE",Details=disks["PD0"]};
  Check(d.ToString()=="PD0  |  4 TB  |  C:, D:  |  SSD MODEL SERIAL FIRMWARE","Full selector label");
  Check(d.Identity=="PD0   SSD MODEL SERIAL FIRMWARE","Command identity untouched by display data");
  WindowsDiskMetadata.ApplyLetters(disks,parts,new[]{new KeyValuePair<string,string>(partition,"G:")});
  Check(disks["PD0"].LettersLabel=="G:"&&disks["PD1"].LettersLabel=="N/A","Refresh clears stale letters");
  Check(d.Handle=="PD0","Physical command target unchanged");
  Console.WriteLine("PASS: "+count+" metadata/display checks");
 }
}

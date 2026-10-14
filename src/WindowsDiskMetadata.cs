using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Text.RegularExpressions;

namespace SeaChestGUI {
    // Display-only data: never used to choose the target of a disk command.
    public sealed class WindowsMetadataSnapshot {
        public Dictionary<string,DiskDetails> Disks=new Dictionary<string,DiskDetails>(StringComparer.OrdinalIgnoreCase);
        public string Notice="";
    }
    public static class WindowsDiskMetadata {
        // Read physical size and partition letters separately: the capacity of a
        // partition (or free space) must never be mistaken for total disk size.
        public static WindowsMetadataSnapshot Read() {
            var result=new WindowsMetadataSnapshot();
            if(Environment.OSVersion.Platform!=PlatformID.Win32NT) {
                result.Notice="Capacidad y letras requieren la información de discos de Windows.";
                return result;
            }
            try {
                ReadRows("SELECT Index, DeviceID, Size FROM Win32_DiskDrive", row=> {
                    if(row["Index"]==null || row["DeviceID"]==null)return;
                    string handle="PD"+Convert.ToUInt32(row["Index"],CultureInfo.InvariantCulture);
                    string expected=@"\\.\PhysicalDrive"+handle.Substring(2);
                    if(!expected.Equals(Convert.ToString(row["DeviceID"]),StringComparison.OrdinalIgnoreCase))return;
                    ulong size;var details=new DiskDetails();
                    if(UInt64.TryParse(Convert.ToString(row["Size"],CultureInfo.InvariantCulture),out size)&&size>0)details.SizeBytes=size;
                    result.Disks[handle]=details;
                });
            } catch(Exception ex) {
                result.Notice="Windows no pudo proporcionar todas las capacidades: "+ex.Message;
                return result;
            }
            try {
                var partitions=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                ReadRows("SELECT DeviceID, DiskIndex FROM Win32_DiskPartition",row=> {
                    if(row["DeviceID"]==null || row["DiskIndex"]==null)return;
                    partitions[Convert.ToString(row["DeviceID"]) ]="PD"+Convert.ToUInt32(row["DiskIndex"],CultureInfo.InvariantCulture);
                });
                var links=new List<KeyValuePair<string,string>>();
                ReadRows("SELECT Antecedent, Dependent FROM Win32_LogicalDiskToPartition",row=> {
                    string part=DeviceIdFromPath(Convert.ToString(row["Antecedent"]));
                    string letter=DeviceIdFromPath(Convert.ToString(row["Dependent"]));
                    if(part==null||letter==null)throw new FormatException("No se pudo interpretar una asociación de particiones de Windows.");
                    links.Add(new KeyValuePair<string,string>(part,letter));
                });
                ApplyLetters(result.Disks,partitions,links);
            } catch(Exception ex) {
                result.Notice="Capacidades disponibles; Windows no pudo proporcionar todas las letras: "+ex.Message;
            }
            return result;
        }
        static void ReadRows(string query,Action<ManagementObject> consume) {
            var scope=new ManagementScope(@"\\.\root\cimv2");
            var options=new EnumerationOptions {ReturnImmediately=true,Rewindable=false,Timeout=TimeSpan.FromSeconds(4)};
            using(var searcher=new ManagementObjectSearcher(scope,new ObjectQuery(query),options))
            using(var rows=searcher.Get())foreach(ManagementObject row in rows)using(row)consume(row);
        }
        public static string DeviceIdFromPath(string path) {
            var m=Regex.Match(path??"",@"(?:^|\.)DeviceID=""((?:\\.|[^""\\])*)""$",RegexOptions.IgnoreCase);
            return m.Success?Regex.Replace(m.Groups[1].Value,@"\\([\\""])","$1"):null;
        }
        // Pure mapping function exercised with multiple partitions and drives.
        public static void ApplyLetters(Dictionary<string,DiskDetails> disks,Dictionary<string,string> partitions,IEnumerable<KeyValuePair<string,string>> links) {
            var grouped=new Dictionary<string,List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach(var link in links) {
                string handle;
                if(!partitions.TryGetValue(link.Key,out handle)||!disks.ContainsKey(handle))continue;
                if(!Regex.IsMatch(link.Value??"",@"^[A-Za-z]:$"))continue;
                if(!grouped.ContainsKey(handle))grouped[handle]=new List<string>();
                grouped[handle].Add(link.Value.ToUpperInvariant());
            }
            foreach(var pair in disks) {
                pair.Value.Letters=grouped.ContainsKey(pair.Key)?grouped[pair.Key].Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray():new string[0];
                pair.Value.LettersKnown=true;
            }
        }
    }
}

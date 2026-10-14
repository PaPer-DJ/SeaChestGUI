using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SeaChestGUI {
    public sealed class Tool {
        public string Path, Name, Hash;
        public List<Option> Options = new List<Option>();
        public string Help = "";
        public override string ToString() { return Name.Replace("openSeaChest_", ""); }
        public static string Digest(string path) {
            using (var s = File.OpenRead(path)) using (var h = SHA256.Create())
                return BitConverter.ToString(h.ComputeHash(s)).Replace("-", "");
        }
        public static List<Tool> Discover(string folder) {
            return Directory.GetFiles(folder, "*.exe", SearchOption.TopDirectoryOnly)
                .Where(p => Regex.IsMatch(System.IO.Path.GetFileName(p), @"^openSeaChest(?:_[A-Za-z0-9]+)?\.exe$", RegexOptions.IgnoreCase))
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .Select(p => new Tool {Path=System.IO.Path.GetFullPath(p), Name=System.IO.Path.GetFileNameWithoutExtension(p), Hash=Digest(p)}).ToList();
        }
    }
    public sealed class Option {
        public string Flag, Signature, Description;
        public bool TakesValue;
        public override string ToString() { return Signature; }
    }
    public sealed class Argument {
        public string Flag;
        public List<string> Values = new List<string>();
        public Argument(string flag, params string[] values) { Flag=flag; Values.AddRange(values); }
    }
    public sealed class DiskDetails {
        public ulong? SizeBytes;
        public bool LettersKnown;
        public string[] Letters=new string[0];
        public string CapacityLabel {
            get {
                if(!SizeBytes.HasValue || SizeBytes.Value==0)return "Capacidad n/d";
                decimal size=SizeBytes.Value;
                string[] units={"B","KB","MB","GB","TB","PB","EB"};int unit=0;
                while(size>=1000 && unit<units.Length-1){size/=1000;unit++;}
                return size.ToString("0.##",System.Globalization.CultureInfo.GetCultureInfo("es-ES"))+" "+units[unit];
            }
        }
        public string LettersLabel {get {return !LettersKnown?"Letras n/d":Letters.Length==0?"N/A":string.Join(", ",Letters);}}
    }
    public sealed class Disk {
        public string Handle, Identity;
        public DiskDetails Details=new DiskDetails();
        public override string ToString() {
            string text=Identity??"";
            if(!string.IsNullOrEmpty(Handle)&&text.StartsWith(Handle+" ",StringComparison.OrdinalIgnoreCase))text=text.Substring(Handle.Length).Trim();
            return Handle+"  |  "+Details.CapacityLabel+"  |  "+Details.LettersLabel+"  |  "+text;
        }
    }
    public static class Protocol {
        static readonly Regex Head = new Regex(@"^(\s+)(-{1,2}[A-Za-z][A-Za-z0-9_-]*)(?=[\s,=/]|$)");
        public static List<Option> ParseHelp(string text) {
            string[] lines=text.Replace("\r", "").Split(new char[] { '\n' }, StringSplitOptions.None);
            int indent=int.MaxValue;
            foreach (string line in lines) {
                var m=Head.Match(line); if(m.Success) indent=Math.Min(indent,m.Groups[1].Value.Replace("\t","    ").Length);
            }
            var result=new List<Option>(); Option current=null;
            foreach (string line in lines) {
                var m=Head.Match(line);
                if(m.Success && m.Groups[1].Value.Replace("\t","    ").Length==indent) {
                    var flags=Regex.Matches(line.Trim(), @"(?<![A-Za-z0-9_-])--?[A-Za-z][A-Za-z0-9_-]*").Cast<Match>().Select(x=>x.Value).ToList();
                    string flag=flags.FirstOrDefault(x=>x.StartsWith("--")) ?? flags[0];
                    var existing=result.FirstOrDefault(o=>o.Flag==flag);
                    if(existing!=null) {current=existing;current.Description+=Environment.NewLine+line.Trim()+Environment.NewLine;}
                    else {
                        current=new Option {Flag=flag, Signature=line.Trim(), Description="", TakesValue=Regex.IsMatch(line, @"\[[^\]]+\]|<[^>]+>")};
                        result.Add(current);
                    }
                } else if(current!=null) current.Description+=line.TrimEnd()+Environment.NewLine;
            }
            foreach(var o in result) o.Description=o.Description.Trim();
            return result;
        }
        public static List<Disk> ParseScan(string text) {
            var result=new List<Disk>();
            bool vendorFirst=false;
            const string handlePattern=@"(?<handle>PD\d+|\\\\\.\\PhysicalDrive\d+|CSMI:[^\s]+)";
            foreach(string line in text.Replace("\r", "").Split(new char[] { '\n' }, StringSplitOptions.None)) {
                // Windows tables have Vendor before Handle; other versions start
                // with Handle. Only accept a vendor prefix after that table header.
                if(Regex.IsMatch(line,@"^\s*Vendor\s+Handle\b",RegexOptions.IgnoreCase)) {
                    vendorFirst=true;continue;
                }
                if(Regex.IsMatch(line,@"^\s*Handle\b",RegexOptions.IgnoreCase)) {
                    vendorFirst=false;continue;
                }
                var m=Regex.Match(line,@"^\s*"+handlePattern+@"\s+(?<details>.+)$",RegexOptions.IgnoreCase);
                if(!m.Success && vendorFirst)
                    m=Regex.Match(line,@"^\s*[^\s-]+\s+"+handlePattern+@"(?: {2,}|\t+)(?<details>.+)$",RegexOptions.IgnoreCase);
                if(!m.Success) continue;
                string h=m.Groups["handle"].Value;
                if(h.StartsWith(@"\\.\PhysicalDrive",StringComparison.OrdinalIgnoreCase))h="PD"+Regex.Match(h,@"\d+$").Value;
                if(result.Any(d=>d.Handle.Equals(h,StringComparison.OrdinalIgnoreCase)))continue;
                result.Add(new Disk {Handle=h,Identity=h+"   "+Regex.Replace(m.Groups["details"].Value.Trim(),@"\s+"," ")});
            }
            return result;
        }
        // Enrich only the displayed scan. Raw output remains the parser's input.
        public static string FormatScanResults(string text,IEnumerable<Disk> disks) {
            var details=disks.ToDictionary(d=>d.Handle,d=>d.Details,StringComparer.OrdinalIgnoreCase);
            int capacityWidth=Math.Max(9,details.Values.Select(d=>d.CapacityLabel.Length).DefaultIfEmpty(0).Max())+2;
            int letterWidth=Math.Max(6,details.Values.Select(d=>d.LettersLabel.Length).DefaultIfEmpty(0).Max())+2;
            string[] lines=text.Replace("\r\n","\n").Replace("\r","\n").Split('\n');
            bool vendorFirst=false,table=false;int column=0;
            for(int i=0;i<lines.Length;i++) {
                var header=Regex.Match(lines[i],@"^\s*(?:Vendor\s+)?Handle\s+(?=Model\b)",RegexOptions.IgnoreCase);
                if(header.Success) {
                    table=true;vendorFirst=Regex.IsMatch(lines[i],@"^\s*Vendor\b",RegexOptions.IgnoreCase);
                    column=header.Length;
                    lines[i]=lines[i].Insert(column,"Capacidad".PadRight(capacityWidth)+"Unidad".PadRight(letterWidth));
                    continue;
                }
                if(!table)continue;
                var row=Regex.Match(lines[i],@"^\s*"+(vendorFirst?@"\S+\s+":"")+@"(?<handle>PD\d+|\\\\\.\\PhysicalDrive\d+|CSMI:[^\s]+)\s+",RegexOptions.IgnoreCase);
                if(row.Success) {
                    string handle=row.Groups["handle"].Value;
                    if(handle.StartsWith(@"\\.\PhysicalDrive",StringComparison.OrdinalIgnoreCase))handle="PD"+Regex.Match(handle,@"\d+$").Value;
                    DiskDetails value;
                    if(details.TryGetValue(handle,out value))
                        lines[i]=lines[i].Insert(row.Length,value.CapacityLabel.PadRight(capacityWidth)+value.LettersLabel.PadRight(letterWidth));
                } else if(lines[i].Length>=column && Regex.IsMatch(lines[i],@"^\s*[-=][-\s=]*$")) {
                    lines[i]=lines[i].Insert(column,new string('-',capacityWidth+letterWidth));
                }
            }
            return string.Join("\n",lines);
        }
        // Each field is one exact argv item. Never invoke a shell or split a path on spaces.
        public static string Quote(string value) {
            var b=new StringBuilder("\""); int slashes=0;
            foreach(char c in value) {
                if(c=='\\') {slashes++;continue;}
                if(c=='"') {b.Append('\\',slashes*2+1);b.Append('"');}
                else {b.Append('\\',slashes);b.Append(c);} slashes=0;
            }
            b.Append('\\',slashes*2);b.Append('"');return b.ToString();
        }
        public static List<string> Build(string handle, IEnumerable<Argument> args) {
            var result=new List<string>();
            if(handle!=null) {
                if(!Regex.IsMatch(handle,@"^(PD\d+|CSMI:[^\s]+)$",RegexOptions.IgnoreCase))throw new ArgumentException("Identificador de disco no válido.");
                result.Add("-d"); result.Add(handle);
            }
            foreach(var a in args) {
                if(!Regex.IsMatch(a.Flag,@"^--?[A-Za-z][A-Za-z0-9_-]*$")) throw new ArgumentException("Opción no válida: "+a.Flag);
                if(a.Flag=="-d" || a.Flag=="--device")throw new ArgumentException("El disco se elige en el selector superior.");
                result.Add(a.Flag);
                foreach(string v in a.Values) {
                    if(v.IndexOf('\0')>=0 || v.IndexOf('\r')>=0 || v.IndexOf('\n')>=0)throw new ArgumentException("Cada valor debe ocupar una línea.");
                    if(Regex.IsMatch(v,@"^-{1,2}[A-Za-z]"))throw new ArgumentException("Añade las opciones por separado, no dentro de un valor: "+v);
                    result.Add(v);
                }
            }
            if(result.Count==0 || (handle!=null && result.Count==2))throw new ArgumentException("Añade al menos una opción.");
            return result;
        }
        public static string Command(string exe,IEnumerable<string> args) {return Quote(exe)+" "+string.Join(" ",args.Select(Quote));}
        public static string ExitText(int code) {
            string[] names={"Sin error comunicado por la herramienta","Opciones incorrectas","Disco no válido o no especificado","La operación ha fallado","Operación no compatible","Operación abortada","Ruta no encontrada","No se puede abrir el archivo","El archivo ya existe","Se requieren permisos de administrador","Recursos insuficientes","Error al escribir el archivo","Disco no encontrado","Disco ocupado","Directorio no seguro"};
            return code>=0 && code<names.Length?names[code]:"Código de salida no reconocido";
        }
    }
    public sealed class RunResult {public int Code; public string Output;}
    public static class Runner {
        public static async Task<RunResult> Run(Tool tool,List<string> args,string workdir,Action<string> output,int timeoutMs) {
            if(Tool.Digest(tool.Path)!=tool.Hash)throw new IOException("El ejecutable ha cambiado. Vuelve a seleccionar la carpeta antes de continuar.");
            var si=new ProcessStartInfo(tool.Path,string.Join(" ",args.Select(Protocol.Quote))) {
                UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,
                RedirectStandardInput=true,WorkingDirectory=workdir
            };
            var buffer=new StringBuilder(); object gate=new object();
            using(var p=new Process {StartInfo=si}) {
                p.Start(); p.StandardInput.Close();
                Func<StreamReader,Task> pump=async reader=> {
                    char[] chars=new char[2048];int n;
                    while((n=await reader.ReadAsync(chars,0,chars.Length).ConfigureAwait(false))>0) {
                        string chunk=new string(chars,0,n);
                        lock(gate) {if(buffer.Length<4000000)buffer.Append(chunk);}
                        if(output!=null)output(chunk);
                    }
                };
                Task a=pump(p.StandardOutput), b=pump(p.StandardError);
                Task wait=Task.Run(()=>p.WaitForExit());
                if(timeoutMs>0 && await Task.WhenAny(wait,Task.Delay(timeoutMs)).ConfigureAwait(false)!=wait) {
                    try {p.Kill();}catch(InvalidOperationException){}
                    await wait.ConfigureAwait(false);await Task.WhenAll(a,b).ConfigureAwait(false);
                    throw new TimeoutException("La consulta no respondió a tiempo. Comprueba la versión y los permisos.");
                }
                await wait.ConfigureAwait(false);await Task.WhenAll(a,b).ConfigureAwait(false);
                return new RunResult {Code=p.ExitCode,Output=buffer.ToString()};
            }
        }
    }
}

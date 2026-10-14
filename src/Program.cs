using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("SeaChest GUI")]
[assembly: System.Reflection.AssemblyDescription("Interfaz independiente en español para openSeaChest")]
[assembly: System.Reflection.AssemblyVersion("0.1.8.0")]
namespace SeaChestGUI {
    static class Program {
        [STAThread] static void Main(string[] args) {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (!EnsureAdministrator(args)) return;
            Application.ThreadException+=(s,e)=>MessageBox.Show(e.Exception.Message,"SeaChest GUI",MessageBoxButtons.OK,MessageBoxIcon.Error);
            Application.Run(new MainForm(false));
        }
        // The embedded UAC manifest is primary. This fallback also handles a
        // launcher that starts a build without the manifest. Never bypass UAC.
        static bool EnsureAdministrator(string[] args) {
            try {
                using (var identity=WindowsIdentity.GetCurrent()) {
                    if (new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator)) return true;
                }
                if (args.Contains("--elevation-requested")) {
                    MessageBox.Show("Windows no ha concedido permisos de administrador. Cierra la aplicación y usa Ejecutar como administrador.", "SeaChest GUI", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
                var start=new ProcessStartInfo(Application.ExecutablePath) {
                    UseShellExecute=true, Verb="runas", Arguments="--elevation-requested",
                    WorkingDirectory=AppDomain.CurrentDomain.BaseDirectory
                };
                using (var elevated=Process.Start(start)) { }
            } catch (System.ComponentModel.Win32Exception ex) {
                string message=ex.NativeErrorCode==1223
                    ? "Has cancelado la solicitud de permisos. La aplicación se cerrará sin ejecutar ninguna herramienta."
                    : "No se pudo solicitar acceso de administrador: " + ex.Message;
                MessageBox.Show(message,"SeaChest GUI",MessageBoxButtons.OK,MessageBoxIcon.Information);
            } catch (Exception ex) {
                MessageBox.Show("No se pudieron comprobar los permisos de administrador: "+ex.Message,"SeaChest GUI",MessageBoxButtons.OK,MessageBoxIcon.Warning);
            }
            return false;
        }
    }
    public sealed partial class MainForm:Form {
        readonly bool demo;
        bool busy;
        List<Tool> tools=new List<Tool>();
        List<Argument> plan=new List<Argument>();
        List<Option> filtered=new List<Option>();
        string folder="", outputFolder;
        TextBox folderBox=new TextBox(), filter=new TextBox(), values=new TextBox(), preview=new TextBox();
        ComboBox disks=new ComboBox(), modules=new ComboBox();
        ListBox catalog=new ListBox(), queue=new ListBox();
        RichTextBox description=new RichTextBox(), log=new RichTextBox();
        Label status=new Label(), optionLabel=new Label(), diskHint=new Label();
        CheckBox useDisk=new CheckBox();
        ProgressBar progress=new ProgressBar();
        Panel work=new Panel(), config=new Panel(), target=new Panel();
        TabControl tabs=new TabControl();
        Button scanButton, runButton;
        readonly System.Windows.Forms.Timer flushTimer=new System.Windows.Forms.Timer();
        readonly System.Text.StringBuilder pending=new System.Text.StringBuilder();
        readonly object pendingGate=new object();
        readonly List<Button> quickButtons=new List<Button>();
        Color navy=Color.FromArgb(19,35,52), green=Color.FromArgb(0,115,102);
        public MainForm(bool demonstration) {
            demo=demonstration;
            Text="SeaChest GUI · LaNaveRETRO · 0.1.8"+(demo?" · DEMOSTRACIÓN":"");
            Font=new Font("Segoe UI",9.5f);BackColor=Color.FromArgb(243,246,249);
            StartPosition=FormStartPosition.CenterScreen;ClientSize=new Size(1040,870);MinimumSize=new Size(1050,780);
            AutoScaleMode=AutoScaleMode.Dpi;
            outputFolder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SeaChestGUI","Resultados");
            BuildUI();
            flushTimer.Interval=100;flushTimer.Tick+=(s,e)=>Flush();flushTimer.Start();
            FormClosing+=(s,e)=> { if(busy) {e.Cancel=true;MessageBox.Show(this,"Hay una operación en curso. Espera a que termine; cerrar el proceso no garantiza detener una operación interna del disco.","Operación en curso");} };
            FormClosed+=(s,e)=>{flushTimer.Stop();flushTimer.Dispose();};
            Shown+=async(s,e)=> {
                if(demo) {SetupDemo();return;}
                try {Directory.CreateDirectory(outputFolder);}catch(Exception ex){Error(ex);return;}
                bool admin=false;
                try {admin=new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);}catch{}
                if(!admin)Append("AVISO: abre la aplicación como administrador si no puedes acceder a los discos.\n");
                await LoadFolder(AppDomain.CurrentDomain.BaseDirectory);
            };
        }
        TableLayoutPanel Table(int cols,params float[] heights) {
            var t=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=cols,RowCount=heights.Length,Margin=Padding.Empty,Padding=Padding.Empty};
            foreach(float h in heights)t.RowStyles.Add(new RowStyle(h<0?SizeType.Percent:SizeType.Absolute,Math.Abs(h)));
            return t;
        }
        Button Button(string text,EventHandler handler,int width=150) {
            var b=new Button {Text=text,Width=width,Height=28,FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=navy,Margin=new Padding(4),Cursor=Cursors.Hand};
            b.FlatAppearance.BorderColor=Color.FromArgb(194,205,214);b.Click+=handler;return b;
        }
        Label Label(string text) {return new Label {Text=text,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true,Margin=new Padding(4,1,4,1)};}
        void BuildUI() {
            var root=Table(1,66,46,62,-58,-42,30);root.Padding=new Padding(12);Controls.Add(root);
            var head=Table(2,-100);head.BackColor=navy;head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,67));head.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33));
            var title=Label("SeaChest GUI");title.Font=new Font(Font.FontFamily,23,FontStyle.Bold);title.ForeColor=Color.White;title.Margin=new Padding(14,0,0,0);head.Controls.Add(title,0,0);
            var brand=Label("LaNaveRETRO  /  Discos y diagnóstico\nInterfaz independiente · openSeaChest");brand.ForeColor=Color.FromArgb(189,218,225);head.Controls.Add(brand,1,0);root.Controls.Add(head,0,0);
            config.Dock=DockStyle.Fill;root.Controls.Add(config,0,1);
            var configRow=Table(3,-100);configRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,140));configRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));configRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,170));config.Controls.Add(configRow);
            configRow.Controls.Add(Label("openSeaChest"),0,0);folderBox.ReadOnly=true;folderBox.Dock=DockStyle.Fill;folderBox.Margin=new Padding(4,10,4,4);configRow.Controls.Add(folderBox,1,0);
            configRow.Controls.Add(Button("Elegir carpeta…",async(s,e)=>{using(var d=new FolderBrowserDialog {Description="Selecciona la carpeta que contiene openSeaChest_*.exe",SelectedPath=folder})if(d.ShowDialog(this)==DialogResult.OK)await LoadFolder(d.SelectedPath);},160),2,0);
            target.Dock=DockStyle.Fill;root.Controls.Add(target,0,2);
            var driveRow=Table(2,34,26);driveRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));driveRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,170));target.Controls.Add(driveRow);
            disks.DropDownStyle=ComboBoxStyle.DropDownList;disks.Dock=DockStyle.Fill;disks.Margin=new Padding(4,4,4,0);disks.SelectedIndexChanged+=(s,e)=>UpdatePreview();driveRow.Controls.Add(disks,0,0);
            disks.DropDown+=(s,e)=> {
                int width=disks.Width;
                foreach(var item in disks.Items)width=Math.Max(width,TextRenderer.MeasureText(item.ToString(),disks.Font).Width+40);
                disks.DropDownWidth=Math.Min(width,Screen.FromControl(disks).WorkingArea.Width-40);
            };
            scanButton=Button("Buscar discos",async(s,e)=>await Scan(),160);scanButton.Height=30;driveRow.Controls.Add(scanButton,1,0);
            diskHint.Text="Busca los discos y selecciona uno por su modelo y número de serie.";diskHint.Dock=DockStyle.Fill;diskHint.ForeColor=Color.FromArgb(68,88,105);diskHint.Margin=new Padding(5,0,0,0);driveRow.Controls.Add(diskHint,0,1);driveRow.SetColumnSpan(diskHint,2);
            work.Dock=DockStyle.Fill;root.Controls.Add(work,0,3);tabs.Dock=DockStyle.Fill;work.Controls.Add(tabs);
            BuildQuick();BuildAdvanced();BuildGuide();BuildHelp();
            var resultPanel=Table(1,36,-100);resultPanel.Margin=new Padding(0,8,0,0);root.Controls.Add(resultPanel,0,4);
            var bar=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false};bar.Controls.Add(new Label {Text="RESULTADOS",ForeColor=green,Font=new Font(Font,FontStyle.Bold),AutoSize=false,Width=110,Height=28,TextAlign=ContentAlignment.MiddleLeft,Margin=new Padding(5,3,8,0)});
            bar.Controls.Add(Button("Guardar registro…",(s,e)=>SaveLog(),155));bar.Controls.Add(Button("Limpiar",(s,e)=>{if(!busy){Flush();log.Clear();}},90));bar.Controls.Add(Button("Carpeta de resultados",(s,e)=>{if(!demo)try {Process.Start(new ProcessStartInfo(outputFolder){UseShellExecute=true});}catch(Exception ex){Error(ex);}},180));resultPanel.Controls.Add(bar,0,0);
            log.Dock=DockStyle.Fill;log.ReadOnly=true;log.BackColor=navy;log.ForeColor=Color.FromArgb(220,236,238);log.Font=new Font("Consolas",10);log.WordWrap=false;log.DetectUrls=false;resultPanel.Controls.Add(log,0,1);
            var bottom=Table(2,-100);bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));bottom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,180));status.Text="Preparado";status.Dock=DockStyle.Fill;status.TextAlign=ContentAlignment.MiddleLeft;bottom.Controls.Add(status,0,0);progress.Dock=DockStyle.Fill;progress.Visible=false;progress.Style=ProgressBarStyle.Marquee;bottom.Controls.Add(progress,1,0);root.Controls.Add(bottom,0,5);
        }
        void BuildQuick() {
            var tab=new TabPage("Tareas habituales") {BackColor=Color.White,Padding=new Padding(12),AutoScroll=true};tabs.TabPages.Add(tab);
            var panel=Table(1,48,-100,43);tab.Controls.Add(panel);
            var intro=Label("Consulta el estado y los datos de tu disco, o inicia una prueba de diagnóstico.\nLos resultados originales de openSeaChest aparecen debajo.");panel.Controls.Add(intro,0,0);
            var grid=Table(3,-25,-25,-25,-25);for(int i=0;i<3;i++)grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.33f));panel.Controls.Add(grid,0,1);
            AddQuick(grid,0,0,"Información del disco","Basics",new[]{"--deviceInfo"},false);
            AddQuick(grid,1,0,"Estado SMART","SMART",new[]{"--smartCheck"},false);
            AddQuick(grid,2,0,"Atributos SMART","SMART",new[]{"--smartAttributes","hybrid"},false);
            AddQuick(grid,0,1,"Estadísticas del dispositivo","SMART",new[]{"--deviceStatistics"},false);
            AddQuick(grid,1,1,"Salud NVMe","SMART",new[]{"--showNVMeHealth"},false);
            AddQuick(grid,2,1,"Estado de energía","PowerControl",new[]{"--checkPowerMode"},false);
            AddQuick(grid,0,2,"Iniciar prueba corta DST","Basics",new[]{"--shortDST"},true);
            AddQuick(grid,1,2,"Iniciar prueba larga DST","SMART",new[]{"--longDST"},true);
            AddQuick(grid,2,2,"Consultar progreso DST","Basics",new[]{"--progress","dst"},false);
            AddQuick(grid,0,3,"Historial de pruebas DST","Logs",new[]{"--showDSTLog"},false);
            AddQuick(grid,1,3,"Información SATA / SAT","Basics",new[]{"--SATInfo"},false);
            AddQuick(grid,2,3,"Opciones avanzadas →",null,null,false);
            var note=Label("Las pruebas DST se ejecutan en el disco. Usa «Consultar progreso DST» y después «Historial».\nLa disponibilidad depende del disco, el adaptador y la versión de openSeaChest.");note.ForeColor=Color.FromArgb(78,99,117);panel.Controls.Add(note,0,2);
        }
        void AddQuick(TableLayoutPanel grid,int x,int y,string text,string module,string[] args,bool test) {
            var b=Button(text,async(s,e)=> {
                if(module==null) {tabs.SelectedIndex=1;return;}
                Tool tool=tools.FirstOrDefault(t=>t.Name.Equals("openSeaChest_"+module,StringComparison.OrdinalIgnoreCase));
                if(tool==null) {MessageBox.Show(this,"No se encontró openSeaChest_"+module+".exe en esta carpeta.");return;}
                if(disks.SelectedItem==null) {MessageBox.Show(this,"Selecciona un disco primero.");return;}
                await ExecuteQuick(tool,args,test);
            });b.Dock=DockStyle.Fill;b.Height=50;b.BackColor=Color.FromArgb(244,248,249);b.Margin=new Padding(6);grid.Controls.Add(b,x,y);if(module!=null)quickButtons.Add(b);
        }
        void BuildAdvanced() {
            var tab=new TabPage("Todas las opciones") {BackColor=Color.White,Padding=new Padding(8)};tabs.TabPages.Add(tab);
            var body=Table(2,-100);body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,67));tab.Controls.Add(body);
            var left=Table(1,24,33,24,32,-100);left.Margin=new Padding(0,0,8,0);body.Controls.Add(left,0,0);
            left.Controls.Add(Label("1 · Herramienta"),0,0);modules.DropDownStyle=ComboBoxStyle.DropDownList;modules.Dock=DockStyle.Fill;modules.SelectedIndexChanged+=async(s,e)=>await ModuleChanged();left.Controls.Add(modules,0,1);
            left.Controls.Add(Label("2 · Buscar opción o descripción"),0,2);filter.Dock=DockStyle.Fill;filter.TextChanged+=(s,e)=>FilterCatalog();left.Controls.Add(filter,0,3);
            catalog.Dock=DockStyle.Fill;catalog.Font=new Font("Consolas",9.5f);catalog.HorizontalScrollbar=true;catalog.SelectedIndexChanged+=(s,e)=>SelectOption();left.Controls.Add(catalog,0,4);
            var right=Table(1,-35,25,58,36,-22,38,38);body.Controls.Add(right,1,0);
            description.ReadOnly=true;description.Dock=DockStyle.Fill;description.BackColor=Color.FromArgb(247,249,250);description.BorderStyle=BorderStyle.FixedSingle;description.Font=new Font("Consolas",9);description.DetectUrls=false;right.Controls.Add(description,0,0);
            optionLabel.Text="3 · Valores de la opción (un argumento por línea; sin comillas)";optionLabel.Dock=DockStyle.Fill;right.Controls.Add(optionLabel,0,1);
            values.Multiline=true;values.ScrollBars=ScrollBars.Vertical;values.Dock=DockStyle.Fill;right.Controls.Add(values,0,2);
            var addBar=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false};addBar.Controls.Add(Button("Elegir archivo…",(s,e)=>{using(var d=new OpenFileDialog())if(d.ShowDialog(this)==DialogResult.OK)values.Text=d.FileName;},135));addBar.Controls.Add(Button("Añadir opción ↓",(s,e)=>AddOption(),145));useDisk.Text="Aplicar al disco seleccionado";useDisk.Checked=true;useDisk.AutoSize=true;useDisk.Margin=new Padding(10,10,0,0);useDisk.CheckedChanged+=(s,e)=>UpdatePreview();addBar.Controls.Add(useDisk);right.Controls.Add(addBar,0,3);
            queue.Dock=DockStyle.Fill;queue.HorizontalScrollbar=true;queue.Font=new Font("Consolas",9);right.Controls.Add(queue,0,4);
            preview.ReadOnly=true;preview.Dock=DockStyle.Fill;preview.Font=new Font("Consolas",9);preview.Multiline=false;preview.WordWrap=false;right.Controls.Add(preview,0,5);
            var actions=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false};actions.Controls.Add(Button("Quitar seleccionada",(s,e)=>{if(queue.SelectedIndex>=0){plan.RemoveAt(queue.SelectedIndex);RefreshPlan();}},160));actions.Controls.Add(Button("Vaciar",(s,e)=>{plan.Clear();RefreshPlan();},85));runButton=Button("Revisar y ejecutar…",async(s,e)=>await ExecutePlan(),175);runButton.BackColor=green;runButton.ForeColor=Color.White;actions.Controls.Add(runButton);right.Controls.Add(actions,0,6);
        }
        void BuildHelp() {
            var tab=new TabPage("Ayuda") {BackColor=Color.White,Padding=new Padding(16)};tabs.TabPages.Add(tab);
            var t=Table(1,-100,40);tab.Controls.Add(t);
            var help=new RichTextBox {ReadOnly=true,Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,Font=new Font("Segoe UI",11),BackColor=Color.White};
            help.Text="CÓMO EMPEZAR\n\n1. Extrae la distribución oficial de openSeaChest para Windows.\n2. Copia SeaChestGUI.exe y su archivo .config junto a openSeaChest_Basics.exe.\n3. Abre SeaChestGUI.exe y acepta los permisos de administrador.\n4. Espera a la búsqueda automática y comprueba modelo y serie antes de seleccionar. Usa Buscar discos para actualizar la lista si conectas otra unidad.\n\nTODAS LAS OPCIONES\n\nEl catálogo se obtiene de --help del ejecutable seleccionado. Su documentación original está en inglés. Selecciona una opción, escribe sus valores (un argumento por línea) y añádela. Las rutas de archivo completas no necesitan comillas. Combina las opciones que indique la ayuda; la GUI no valida compatibilidades del firmware. --device se gestiona con el selector de disco. Se trabaja con una unidad cada vez; no se admite «all».\n\nBorrado, formato, firmware, seguridad y comandos desconocidos pueden modificar datos. El modo avanzado requiere revisión y confirmación. Si la ayuda pide --confirm, añade esa opción con el texto exacto indicado; no se añade de forma automática. Antes de un cambio se vuelve a comprobar el disco detectado. No desconectes discos durante la operación.\n\nRESULTADOS Y OPERACIONES EN SEGUNDO PLANO\n\nEl código de salida describe el resultado del proceso, no garantiza que una operación interna haya finalizado. Consulta su progreso con el módulo correspondiente. La ventana se mantiene abierta mientras el proceso está activo; no se fuerza su cancelación. El búfer visible es limitado. Puedes guardar lo visible en un TXT; puede incluir números de serie o información sensible. Los archivos que genere la herramienta se escriben en la carpeta de resultados indicada al inicio.\n\nEsta es una interfaz independiente, no un producto oficial de Seagate. Versión 0.1.8: compilación y pruebas de software realizadas en Linux/Mono; pendiente de validación con discos físicos en Windows. Sin telemetría ni descargas automáticas. Solo ejecuta herramientas oficiales de una carpeta de confianza.";
            t.Controls.Add(help,0,0);t.Controls.Add(Button("Ver ayuda completa del módulo",(s,e)=>{var tool=modules.SelectedItem as Tool;if(tool!=null){var f=new Form {Text=tool.Name+" · ayuda original",Size=new Size(900,700),StartPosition=FormStartPosition.CenterParent};f.Controls.Add(new RichTextBox {Text=tool.Help,ReadOnly=true,Dock=DockStyle.Fill,Font=new Font("Consolas",10),WordWrap=false});f.ShowDialog(this);}},280),0,1);
        }
        async Task LoadFolder(string path) {
            if(busy)return;SetBusy(true,"Buscando ejecutables…");
            bool loaded=false;
            try {
                tools=await Task.Run(()=>Tool.Discover(path));folder=path;folderBox.Text=path;disks.Items.Clear();plan.Clear();RefreshPlan();modules.Items.Clear();
                foreach(var t in tools)modules.Items.Add(t);
                Append("\nCarpeta: "+path+"\nMódulos encontrados: "+tools.Count+"\nArchivos de salida: "+outputFolder+"\n");
                if(tools.Count==0)Append("Copia la GUI junto a los ejecutables originales, o pulsa Elegir carpeta.\n");
                // Select while busy so the async selection event does not race
                // the initial scan. Await both operations in sequence below.
                if(tools.Count>0)modules.SelectedIndex=Math.Max(0,tools.FindIndex(t=>t.Name=="openSeaChest_Basics"));
                loaded=true;
            }catch(Exception ex){Error(ex);}finally{SetBusy(false,"Selecciona un módulo o busca discos");}
            if(loaded && tools.Count>0) {
                await ModuleChanged();
                await Scan();
            }
        }
        async Task<bool> EnsureHelp(Tool t) {
            if(t.Help.Length>0)return true;
            var r=await Runner.Run(t,new List<string>{"--help"},outputFolder,null,25000);
            if(r.Code!=0)throw new IOException("No se pudo leer la ayuda de "+t.Name+". "+Protocol.ExitText(r.Code)+"\n"+r.Output);
            t.Help=r.Output;t.Options=Protocol.ParseHelp(r.Output);
            if(t.Options.Count==0)throw new IOException("No se reconoció el formato de ayuda. Consulta la ayuda completa.");
            return true;
        }
        async Task ModuleChanged() {
            var t=modules.SelectedItem as Tool;if(t==null||busy)return;
            plan.Clear();RefreshPlan();filter.Clear();catalog.Items.Clear();SetBusy(true,"Leyendo opciones de "+t+"…");
            try {await EnsureHelp(t);FilterCatalog();}catch(Exception ex){Error(ex);}finally{SetBusy(false,"Catálogo: "+t.Options.Count+" opciones");}
        }
        void FilterCatalog() {
            var t=modules.SelectedItem as Tool;catalog.BeginUpdate();catalog.Items.Clear();
            filtered=t==null?new List<Option>():t.Options.Where(o=>(o.Signature+" "+o.Description).IndexOf(filter.Text,StringComparison.OrdinalIgnoreCase)>=0).ToList();
            foreach(var o in filtered)catalog.Items.Add(o);catalog.EndUpdate();if(catalog.Items.Count>0)catalog.SelectedIndex=0;else description.Clear();
        }
        void SelectOption() {
            var o=catalog.SelectedItem as Option;if(o==null)return;
            description.Text=o.Signature+"\n\n"+o.Description;values.Clear();values.Enabled=o.Flag!="--device";
            if(o.Flag=="--device")description.Text+="\n\nGUI: usa el selector superior; se opera sobre un disco cada vez.";
            optionLabel.Text=o.TakesValue?"3 · Introduce los valores indicados arriba (un argumento por línea)":"3 · Sin valor salvo que la ayuda lo indique (un argumento por línea)";
        }
        void AddOption() {
            var o=catalog.SelectedItem as Option;if(o==null)return;
            if(o.Flag=="--device") {MessageBox.Show(this,"Elige el disco en el selector superior.");return;}
            var vals=values.Text.Replace("\r","").Split(new char[] { '\n' }, StringSplitOptions.None).Where(v=>v.Length>0).ToArray();
            if(o.TakesValue && vals.Length==0) {if(MessageBox.Show(this,"La ayuda muestra un parámetro. ¿Añadir sin valor? Hazlo solo si la ayuda indica que es opcional.","Revisar valor",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;}
            var arg=new Argument(o.Flag,vals);
            try {Protocol.Build(null,new[]{arg});plan.Add(arg);RefreshPlan();}catch(Exception ex){Error(ex);}
        }
        void RefreshPlan() {queue.Items.Clear();foreach(var a in plan)queue.Items.Add(a.Flag+(a.Values.Count>0?"   "+string.Join(" | ",a.Values):""));UpdatePreview();}
        void UpdatePreview() {
            var t=modules.SelectedItem as Tool;var d=disks.SelectedItem as Disk;
            if(t==null||plan.Count==0){preview.Text="La operación preparada aparecerá aquí.";return;}
            try {preview.Text=Protocol.Command(Path.GetFileName(t.Path),Protocol.Build(useDisk.Checked?(d==null?"PD0":d.Handle):null,plan));if(useDisk.Checked&&d==null)preview.Text="Selecciona un disco para preparar la operación.";}
            catch(Exception ex){preview.Text=ex.Message;}
        }
        Tool ScanTool() {return tools.FirstOrDefault(t=>t.Name=="openSeaChest_Basics")??tools.FirstOrDefault();}
        async Task Scan() {
            if(busy)return;var t=ScanTool();if(t==null){MessageBox.Show(this,"Selecciona la carpeta de openSeaChest primero.");return;}
            if(demo)return;
            SetBusy(true,"Buscando discos…");disks.Items.Clear();
            var scanOutput=new System.Text.StringBuilder();var scanGate=new object();bool displayed=false;
            try {
                var r=await Runner.Run(t,new List<string>{"--scan"},outputFolder,chunk=>{lock(scanGate)scanOutput.Append(chunk);},45000);
                if(r.Code!=0)throw new IOException(Protocol.ExitText(r.Code));
                var found=Protocol.ParseScan(r.Output);
                if(found.Count>0) {
                    SetBusy(true,"Consultando capacidad y letras de unidad…");
                    var metadataTask=Task.Run(()=>WindowsDiskMetadata.Read());
                    if(await Task.WhenAny(metadataTask,Task.Delay(12000))==metadataTask) {
                        var metadata=await metadataTask;
                        foreach(var disk in found) {
                            DiskDetails details;
                            if(metadata.Disks.TryGetValue(disk.Handle,out details))disk.Details=details;
                        }
                        if(metadata.Notice.Length>0)Append("Aviso: "+metadata.Notice+"\n");
                    } else {
                        Append("Windows ha tardado en responder. Se muestran los discos con capacidad y letras no disponibles; puedes volver a buscar.\n");
                    }
                }
                Append(Protocol.FormatScanResults(r.Output,found));displayed=true;
                foreach(var d in found)disks.Items.Add(d);
                diskHint.Text=disks.Items.Count+" discos · Capacidad total (GB/TB) · Letras actuales · Comprueba modelo y serie antes de operar.";
                if(disks.Items.Count==0)Append("No se reconocieron discos. Revisa la salida, los permisos y la conexión.\n");
            }catch(Exception ex){if(!displayed){lock(scanGate)Append(scanOutput.ToString());}Error(ex);}finally{SetBusy(false,"Búsqueda terminada");}
        }
        async Task ExecuteQuick(Tool t,string[] raw,bool test) {
            if(busy||demo)return;var d=disks.SelectedItem as Disk;if(d==null)return;
            SetBusy(true,"Comprobando opción…");
            try {
                await EnsureHelp(t);
                // Resolve capitalization from the installed help, important for NVMe variants.
                var o=t.Options.FirstOrDefault(x=>x.Flag==raw[0])??t.Options.FirstOrDefault(x=>x.Flag.Equals(raw[0],StringComparison.OrdinalIgnoreCase));
                if(o==null)throw new InvalidOperationException("Esta versión no anuncia "+raw[0]+". Utiliza el catálogo de opciones.");
                if(test && MessageBox.Show(this,"Iniciar "+o.Flag+" sobre:\n\n"+d.Identity+"\n\nLa prueba puede durar minutos u horas. El disco seguirá trabajando en segundo plano; consulta su progreso. ¿Continuar?","Iniciar diagnóstico",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
                if(!await Revalidate(d))return;
                var args=Protocol.Build(d.Handle,new[]{new Argument(o.Flag,raw.Skip(1).ToArray())});
                await RunOperation(t,args,d,false,test);
            }catch(Exception ex){Error(ex);}finally{SetBusy(false,"Preparado");}
        }
        async Task<bool> Revalidate(Disk d) {
            var t=ScanTool();if(t==null)throw new IOException("No hay herramienta de detección.");
            var r=await Runner.Run(t,new List<string>{"--scan"},outputFolder,null,45000);
            if(r.Code!=0)throw new IOException("No se pudo volver a comprobar el disco. Ejecuta Buscar discos.");
            var found=Protocol.ParseScan(r.Output).FirstOrDefault(x=>x.Handle.Equals(d.Handle,StringComparison.OrdinalIgnoreCase));
            if(found==null || found.Identity!=d.Identity)throw new IOException("El disco ha cambiado o ya no está conectado. Vuelve a buscar los discos y selecciona el correcto.");
            return true;
        }
        async Task ExecutePlan() {
            if(busy||demo)return;var t=modules.SelectedItem as Tool;var d=useDisk.Checked?disks.SelectedItem as Disk:null;
            if(t==null || plan.Count==0){MessageBox.Show(this,"Elige un módulo y añade opciones.");return;}
            if(useDisk.Checked&&d==null){MessageBox.Show(this,"Selecciona un disco.");return;}
            SetBusy(true,"Revisando operación avanzada…");
            try {
                var args=Protocol.Build(d==null?null:d.Handle,plan);
                bool queryOnly=plan.All(a=>(a.Flag=="--help"||a.Flag=="--version"||a.Flag=="--license"||a.Flag=="--scan")&&a.Values.Count==0);
                if(!queryOnly) {
                    if(d!=null) {
                        await Revalidate(d);
                        bool system=await Task.Run(()=>IsSystemDisk(d.Handle));
                        if(system)throw new InvalidOperationException("El disco contiene Windows. Esta versión bloquea las operaciones avanzadas sobre él. Las consultas habituales sí están disponibles.");
                    }
                    if(!Confirm(t,args,d))return;
                    if(d!=null)await Revalidate(d);
                }
                await RunOperation(t,args,d,!queryOnly);
            }catch(Exception ex){Error(ex);}finally{SetBusy(false,"Preparado");}
        }
        static bool IsSystemDisk(string handle) {
            if(!handle.StartsWith("PD",StringComparison.OrdinalIgnoreCase))return false;
            // A positive match blocks changes. WMI absence is not represented as proof of safety.
            try {
                string system=Path.GetPathRoot(Environment.SystemDirectory).TrimEnd('\\');
                using(var logical=new ManagementObject("Win32_LogicalDisk.DeviceID='"+system+"'"))
                using(var parts=logical.GetRelated("Win32_DiskPartition"))
                    foreach(ManagementObject p in parts)using(p) {
                        if(Convert.ToInt32(p["DiskIndex"])==int.Parse(handle.Substring(2)))return true;
                    }
            }catch{} return false;
        }
        bool Confirm(Tool t,List<string> args,Disk d) {
            string phrase=d==null?"EJECUTAR":"EJECUTAR "+d.Handle;
            using(var f=new Form {Text="Confirmar operación avanzada",Size=new Size(760,540),MinimumSize=new Size(680,480),StartPosition=FormStartPosition.CenterParent,Font=Font,BackColor=Color.White,MinimizeBox=false,MaximizeBox=false}) {
                var layout=Table(1,72,62,-100,52,38,45);layout.Padding=new Padding(16);f.Controls.Add(layout);
                var warning=Label("Esta operación puede modificar la configuración, borrar datos o actualizar el firmware. Revisa la ayuda y la unidad elegida antes de continuar.");warning.ForeColor=Color.FromArgb(160,48,32);warning.Font=new Font(Font,FontStyle.Bold);layout.Controls.Add(warning,0,0);
                layout.Controls.Add(Label(d==null?"Sin disco asignado por la GUI.":"DISCO: "+d.Identity+"\nVerifica modelo y serie; la comprobación del disco de Windows puede no estar disponible."),0,1);
                layout.Controls.Add(new TextBox {Text=Protocol.Command(t.Path,args),Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Both,Font=new Font("Consolas",10)},0,2);
                var check=new CheckBox {Text="He revisado la operación, el disco y la copia de mis datos.",Dock=DockStyle.Fill};layout.Controls.Add(check,0,3);
                var text=new TextBox {Dock=DockStyle.Fill};var row=Table(2,-100);row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,260));row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));row.Controls.Add(Label("Escribe exactamente: "+phrase),0,0);row.Controls.Add(text,1,0);layout.Controls.Add(row,0,4);
                var buttons=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};var ok=Button("Ejecutar",(s,e)=>{f.DialogResult=DialogResult.OK;f.Close();},125);ok.Enabled=false;ok.BackColor=Color.FromArgb(160,48,32);ok.ForeColor=Color.White;var cancel=Button("Cancelar",(s,e)=>{f.DialogResult=DialogResult.Cancel;f.Close();},125);buttons.Controls.Add(cancel);buttons.Controls.Add(ok);layout.Controls.Add(buttons,0,5);f.CancelButton=cancel;
                EventHandler validate=(s,e)=>ok.Enabled=check.Checked&&text.Text==phrase;check.CheckedChanged+=validate;text.TextChanged+=validate;
                return f.ShowDialog(this)==DialogResult.OK;
            }
        }
        async Task RunOperation(Tool t,List<string> args,Disk d,bool advanced,bool startsTest=false) {
            SetBusy(true,"Ejecutando "+t+"…");
            Append("\n────────────────────────────────────────\n"+DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")+" · "+t.Name+"\n"+(d==null?"":d.Identity+"\n"));
            // Do not copy password/key arguments into the GUI history automatically.
            Append(advanced?"Operación avanzada confirmada. Parámetros omitidos del registro.\n":Protocol.Command(Path.GetFileName(t.Path),args)+"\n");
            var r=await Runner.Run(t,args,outputFolder,Append,0);
            string message;
            if(r.Code==0) {
                if(startsTest)message="Prueba solicitada correctamente. Consulta «Consultar progreso DST» para ver su avance y «Historial de pruebas DST» para conocer el resultado.";
                else if(advanced)message="Solicitud procesada sin errores. Revisa la información anterior para conocer el estado de la operación.";
                else message="Consulta finalizada correctamente.";
            } else if(r.Code==5)message="La operación se ha cancelado. Consulta los detalles anteriores. (Código 5)";
            else message="No se pudo completar la operación: "+Protocol.ExitText(r.Code)+". Consulta los detalles anteriores. (Código "+r.Code+")";
            Append("\n"+message+"\n");
            if(r.Code!=0)MessageBox.Show(this,message,"Resultado de la operación",MessageBoxButtons.OK,MessageBoxIcon.Warning);
        }
        void SetBusy(bool value,string message) {busy=value;config.Enabled=!value;target.Enabled=!value;work.Enabled=!value;progress.Visible=value;status.Text=message;}
        void Append(string s) {lock(pendingGate)pending.Append(s);}
        void Flush() {string s;lock(pendingGate){s=pending.ToString();pending.Clear();}if(s.Length==0)return;if(log.TextLength+s.Length>1500000){log.Clear();log.AppendText("[Búfer anterior descartado por tamaño]\n");}log.AppendText(s.Replace("\r\n","\n").Replace("\r","\n"));log.SelectionStart=log.TextLength;log.ScrollToCaret();}
        void Error(Exception ex) {Append("\nERROR: "+ex.Message+"\n");MessageBox.Show(this,ex.Message,"SeaChest GUI",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        void SaveLog() {Flush();using(var d=new SaveFileDialog {Filter="Registro de texto (*.txt)|*.txt",FileName="SeaChest_"+DateTime.Now.ToString("yyyyMMdd_HHmmss")+".txt"})if(d.ShowDialog(this)==DialogResult.OK)try {File.WriteAllText(d.FileName,log.Text,System.Text.Encoding.UTF8);}catch(Exception ex){Error(ex);}}
        void SetupDemo() {
            folderBox.Text=@"C:\openSeaChest\";diskHint.Text="DEMOSTRACIÓN · datos ficticios · ninguna operación se ejecuta";
            disks.Items.Add(new Disk {Handle="PD1",Identity="PD1   ST8000VN004   SERIE-DEMO   SC60",Details=new DiskDetails {SizeBytes=8000000000000UL,LettersKnown=true,Letters=new[]{"D:","E:"}}});disks.SelectedIndex=0;
            var t=new Tool {Name="openSeaChest_Basics",Path=@"C:\openSeaChest\openSeaChest_Basics.exe",Help="Ayuda de demostración"};
            t.Options.Add(new Option {Flag="--deviceInfo",Signature="--deviceInfo",Description="Show information and features for the storage device"});
            tools.Add(t);busy=true;modules.Items.Add(t);modules.SelectedIndex=0;busy=false;FilterCatalog();
            var demoScan="Vendor   Handle   Model Number     Serial Number   FwRev\nSATA     PD1      ST8000VN004      SERIE-DEMO      SC60\nUSB      PD2      USB DEMO         SERIE-USB       1.0\n";
            var demoDisks=new[]{(Disk)disks.Items[0],new Disk {Handle="PD2",Details=new DiskDetails {SizeBytes=500000000000UL,LettersKnown=true}}};
            Append("DEMOSTRACIÓN VISUAL · Sin acceso a discos\n\n"+Protocol.FormatScanResults(demoScan,demoDisks));Flush();status.Text="Demostración · 0.1.8";
        }
    }
}

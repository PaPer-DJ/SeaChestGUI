using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace SeaChestGUI {
    public sealed partial class MainForm {
        void BuildGuide() {
            var tab=new TabPage("Guía de Uso") {BackColor=Color.White,Padding=new Padding(10)};
            tabs.TabPages.Add(tab);
            var layout=Table(1,34,-100,38);tab.Controls.Add(layout);
            var chapters=new ComboBox {Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
            chapters.Items.AddRange(GuideContent.Titles);layout.Controls.Add(chapters,0,0);
            var reader=new RichTextBox {Dock=DockStyle.Fill,ReadOnly=true,BackColor=Color.White,BorderStyle=BorderStyle.None,DetectUrls=true,ScrollBars=RichTextBoxScrollBars.Vertical};
            reader.LinkClicked+=(s,e)=>OpenGuideLink(e.LinkText);
            layout.Controls.Add(reader,0,1);
            Action loadChapter=()=> {
                if(chapters.SelectedIndex<0)return;
                reader.Rtf=GuideContent.Rtf[chapters.SelectedIndex];
                reader.Select(0,0);reader.ScrollToCaret();
            };
            chapters.SelectedIndexChanged+=(s,e)=>{if(reader.IsHandleCreated)loadChapter();};
            reader.HandleCreated+=(s,e)=>reader.BeginInvoke(loadChapter);
            var actions=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=false};
            actions.Controls.Add(Button("Abrir guía PDF",(s,e)=> {
                string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Guia_de_Uso_SeaChestGUI_v0.1.9.pdf");
                if(!File.Exists(path)) {MessageBox.Show(this,"Copia Guia_de_Uso_SeaChestGUI_v0.1.9.pdf del ZIP junto a SeaChestGUI.exe. La guía integrada sigue disponible aquí.","Guía PDF",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
                try {Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(this,"No se pudo abrir el PDF: "+ex.Message,"Guía PDF",MessageBoxButtons.OK,MessageBoxIcon.Information);}
            },150));
            actions.Controls.Add(Button("Ampliar lectura",(s,e)=> {
                using(var f=new Form {Text=GuideContent.Titles[chapters.SelectedIndex],Size=new Size(900,760),MinimumSize=new Size(650,480),StartPosition=FormStartPosition.CenterParent}) {
                    var full=new RichTextBox {ReadOnly=true,Dock=DockStyle.Fill,BackColor=Color.White,BorderStyle=BorderStyle.None,DetectUrls=true};
                    full.LinkClicked+=(sender,link)=>OpenGuideLink(link.LinkText);f.Padding=new Padding(18);f.Controls.Add(full);f.Shown+=(sender,args)=>{full.Rtf=GuideContent.Rtf[chapters.SelectedIndex];full.Select(0,0);full.ScrollToCaret();};f.ShowDialog(this);
                }
            },150));
            layout.Controls.Add(actions,0,2);chapters.SelectedIndex=0;
        }
        void OpenGuideLink(string link) {
            Uri url;
            if(!Uri.TryCreate(link,UriKind.Absolute,out url)||url.Scheme!=Uri.UriSchemeHttps)return;
            try {Process.Start(new ProcessStartInfo(url.AbsoluteUri){UseShellExecute=true});}catch(Exception ex){MessageBox.Show(this,"No se pudo abrir el enlace: "+ex.Message,"Guía de Uso",MessageBoxButtons.OK,MessageBoxIcon.Information);}
        }
    }
}

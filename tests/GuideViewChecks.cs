using System;using System.Linq;using System.Collections.Generic;using System.Windows.Forms;using SeaChestGUI;
class GuideViewChecks {
 static IEnumerable<Control> All(Control c){foreach(Control x in c.Controls){yield return x;foreach(var y in All(x))yield return y;}}
 [STAThread] static void Main(){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  Application.ThreadException+=(s,e)=>{Console.WriteLine(e.Exception);Environment.Exit(1);};
  using(var f=new MainForm(true)) {
   f.Show();Application.DoEvents();var tabs=All(f).OfType<TabControl>().First();
   if(tabs.TabCount!=4||tabs.TabPages[2].Text!="Guía de Uso"||tabs.TabPages[3].Text!="Ayuda")throw new Exception("Tab order");
   tabs.SelectedIndex=2;Application.DoEvents();var tab=tabs.TabPages[2];var combo=All(tab).OfType<ComboBox>().First();var reader=All(tab).OfType<RichTextBox>().First();
   for(int i=0;i<combo.Items.Count;i++){combo.SelectedIndex=i;Application.DoEvents();if(!reader.Text.Contains(combo.Items[i].ToString())||reader.Text.Length<200)throw new Exception("Chapter "+i);}
   combo.SelectedIndex=0;Application.DoEvents();
   if(!reader.Text.Contains("Por qué nació esta interfaz")||!reader.Text.Contains("Los platos pueden seguir girando")||!reader.Text.Contains("stalls"))throw new Exception("Updated embedded guide");
   if(typeof(MainForm).Assembly.GetName().Version.ToString()!="0.1.9.0")throw new Exception("Assembly version");
   tabs.SelectedIndex=3;Application.DoEvents();if(!All(tabs.TabPages[3]).OfType<RichTextBox>().First().Text.Contains("POR QUÉ NACIÓ ESTA INTERFAZ"))throw new Exception("Updated Help");
   tabs.SelectedIndex=2;Application.DoEvents();
   var timer=new Timer {Interval=200};bool modal=false;
   timer.Tick+=(s,e)=>{foreach(Form window in Application.OpenForms){if(window!=f){modal=All(window).OfType<RichTextBox>().Any(r=>r.Text.Contains("Si simplemente"));window.Close();break;}}timer.Stop();};
   timer.Start();All(tab).OfType<Button>().First(b=>b.Text=="Ampliar lectura").PerformClick();timer.Dispose();if(!modal)throw new Exception("Expanded reading");
   Console.WriteLine("PASS: version 0.1.9, updated embedded guide and Help, four tabs, 15 guide chapters loaded, expanded reading.");f.Close();
  }
 }
}

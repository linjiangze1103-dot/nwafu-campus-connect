using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using System.Drawing;
using NwafuConnect;
class LayoutTests {
 static int errors,checks;
 static bool Content(Control c){return c is Label || c is ButtonBase || c is TextBox;}
 static void Verify(Control parent){
  foreach(Control c in parent.Controls){
   if(Content(c)){
    checks++;if(!parent.ClientRectangle.Contains(c.Bounds)){errors++;Console.WriteLine("FAIL outside parent: "+c.Text+" "+c.Bounds);}
    foreach(Control other in parent.Controls)if(other!=c && Content(other) && parent.Controls.GetChildIndex(other)>parent.Controls.GetChildIndex(c) && c.Bounds.IntersectsWith(other.Bounds)){errors++;Console.WriteLine("FAIL overlap: "+c.Text+" / "+other.Text);}
    if(c is Label && c.Text.Length>0){var measured=TextRenderer.MeasureText(c.Text,c.Font,new Size(c.ClientSize.Width,Int32.MaxValue),TextFormatFlags.WordBreak);if(measured.Height>c.ClientSize.Height){errors++;Console.WriteLine("FAIL clipped text: "+c.Text);}}
   } Verify(c);
  }
 }
 [STAThread] static int Main(){
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"layout-test-"+Guid.NewGuid().ToString("N"));
  try{using(var f=new MainWindow(new Store(folder),false)){
   ((Timer)typeof(MainWindow).GetField("timer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f)).Stop();
   f.Show();Application.DoEvents();Console.WriteLine("Actual desktop DPI: "+f.CurrentAutoScaleDimensions.Width);Verify(f);
  }}finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
  Console.WriteLine(checks+" content bounds checked, "+errors+" layout failures");return errors==0?0:1;
 }
}

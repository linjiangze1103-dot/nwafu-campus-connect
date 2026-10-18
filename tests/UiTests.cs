using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
using NwafuConnect;
public static class UiTests {
 static int count;
 static T Field<T>(object o,string name){return (T)o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(o);}
 static void Check(bool condition,string label){if(!condition)throw new Exception("FAIL: "+label);count++;Console.WriteLine("PASS: "+label);}
 [STAThread] public static int Main(){
  const string runKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run",runName="NWAFU-CampusConnect";
  string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"ui-storage-"+Guid.NewGuid().ToString("N"));
  object original;using(var key=Registry.CurrentUser.CreateSubKey(runKey)){original=key.GetValue(runName);}
  MainWindow form=null;
  try {
   Application.EnableVisualStyles();var store=new Store(folder);form=new MainWindow(store,false,null,null);
   Field<Timer>(form,"timer").Stop();form.Show();
   Field<TextBox>(form,"username").Text="fixture-do-not-authenticate";Field<TextBox>(form,"password").Text="fixture-secret";
   Field<Button>(form,"begin").PerformClick();
   var state=Field<MonitorState>(form,"state");Check(state.CanAttempt,"save/start enables monitoring");Check(store.HasAccount,"save/start encrypts account locally");
   using(var key=Registry.CurrentUser.CreateSubKey(runKey)){key.DeleteValue(runName,false);}
   Field<Button>(form,"autoButton").PerformClick();
   using(var key=Registry.CurrentUser.OpenSubKey(runKey)){Check(key.GetValue(runName)!=null,"startup button adds current-user login entry");}
   Field<Button>(form,"autoButton").PerformClick();
   using(var key=Registry.CurrentUser.OpenSubKey(runKey)){Check(key.GetValue(runName)==null,"disable startup removes login entry");}
   Check(state.CanAttempt,"disabling startup does not stop active monitoring");
   form.Close();Check(!form.Visible && !form.IsDisposed,"window close hides rather than exits");Check(state.CanAttempt,"window close retains monitoring");
   form.Show();Check(form.Visible,"hidden window can reopen");
   Field<Button>(form,"stop").PerformClick();Check(!state.CanAttempt,"stop button stops automatic authentication");
   Check(store.LoadPreferences().Paused,"explicit stop survives next automatic launch");
   Console.WriteLine(count+" GUI behavior checks passed; timer disabled, no network requests made.");return 0;
  }catch(Exception e){Console.WriteLine(e);return 1;}
  finally{if(form!=null)form.Dispose();using(var key=Registry.CurrentUser.CreateSubKey(runKey)){if(original==null)key.DeleteValue(runName,false);else key.SetValue(runName,original);}
   if(Directory.Exists(folder))Directory.Delete(folder,true);}
 }
}



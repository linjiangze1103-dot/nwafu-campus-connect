using System;
using System.IO;
using System.Diagnostics;
using System.Threading;
using NwafuConnect;
public static class FastTests {
 public static int Count;
 public static void Check(bool ok,string name){if(!ok)throw new Exception("FAIL "+name);Count++;Console.WriteLine("PASS "+name);}
 public static string Temp(){var p=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"fixture-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(p);return p;}
 public static bool Until(Func<bool> condition,int ms){var w=Stopwatch.StartNew();while(w.ElapsedMilliseconds<ms){if(condition())return true;Thread.Sleep(10);}return condition();}
 [STAThread] public static int Main(string[] args){try{if(args.Length>0 && args[0]=="--hang"){Thread.Sleep(30000);return 0;}if(args.Length>0 && args[0]=="--output"){Console.Write(new string('x',100000));return 0;}if(args.Length>0 && args[0]=="--reject"){Console.Error.WriteLine("login failed with error: E2553");return 0;}string suite=args.Length==0?"All":args[0];foreach(var t in typeof(FastTests).Assembly.GetTypes()){if(t.Name.EndsWith("Tests") && t!=typeof(FastTests) && (suite=="All" || (suite=="CI" && t.Name!="HostTests") || t.Name==suite+"Tests")){var m=t.GetMethod("Run");if(m!=null){SynchronizationContext.SetSynchronizationContext(null);m.Invoke(null,null);SynchronizationContext.SetSynchronizationContext(null);}}}Console.WriteLine(Count+" checks passed");return Count==0?2:0;}catch(Exception e){Console.WriteLine(e);return 1;}}
}

using System;
using NwafuConnect;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;
using System.Collections.Generic;
public static class Tests {
 static int failures=0, assertions=0;
 static void Equal<T>(T expected,T actual,string label) { assertions++; if(!object.Equals(expected,actual)) { failures++; Console.WriteLine("FAIL "+label+": expected "+expected+", got "+actual); } }
 public static int Main() {
  Equal(AuthResult.PasswordError,Policy.Classify("login failed: password_error"),"wrong password must pause retries");
  Equal(AuthResult.PasswordError,Policy.Classify("用户名或密码错误"),"Chinese credential rejection");
  Equal(AuthResult.PasswordError,Policy.Classify("login failed with error: E2553"),"school E2553 credential rejection");
  Equal(AuthResult.PasswordError,Policy.Classify("login failed with error: E2531"),"school nonexistent account must not be retried");
  Equal(AuthResult.DeviceLimit,Policy.Classify("login failed with error: E2620"),"school E2620 device limit");
  Equal(AuthResult.DeviceLimit,Policy.Classify("login failed: online_user_full"),"online-device limit");
  Equal(AuthResult.NetworkError,Policy.Classify("context deadline exceeded"),"timeout is retryable");
  Equal(AuthResult.Success,Policy.Classify("[INFO] 登录成功~"),"successful engine login");
  Equal(AuthResult.Rejected,Policy.Classify("login failed with error: E9999"),"unknown auth rejection must not repeatedly try password");
  Equal(PortalState.Online,Policy.ParsePortal("nwafuProbe({\"error\":\"ok\",\"user_name\":\"fixture\",\"online_ip\":\"10.0.1.2\"})"),"authenticated portal");
  Equal(PortalState.Offline,Policy.ParsePortal("nwafuProbe({\"error\":\"not_online_error\",\"client_ip\":\"10.0.1.2\"})"),"offline portal");
  Equal(PortalState.Invalid,Policy.ParsePortal("<html>sign in to hotel wifi</html>"),"foreign captive portal must not receive credentials");
  Equal(PortalState.Invalid,Policy.ParsePortal("{\"error\":\"ok\"}"),"incomplete portal response");
  Equal(PortalState.Invalid,Policy.ParsePortal("{\"error\":\"maintenance\",\"client_ip\":\"10.0.1.2\"}"),"unknown portal response must not trigger login");
  Equal(true,Policy.IsCampusAddress("172.26.8.11"),"school private gateway");
  Equal(false,Policy.IsCampusAddress("127.0.0.1"),"loopback must not count as school network");
  Equal(false,Policy.IsCampusAddress("8.8.8.8"),"public DNS answer must not trigger login");
  Equal(3,Policy.RetrySeconds(1),"first transient failure");
  Equal(3,Policy.RetrySeconds(2),"backoff increases");
  Equal(600,Policy.RetrySeconds(100),"retry interval is bounded without overflow");
  var s=new MonitorState(); s.Start(); Equal(true,s.CanAttempt,"start enables monitoring");
  s.Observe(AuthResult.PasswordError); Equal(false,s.CanAttempt,"password error blocks automatic retries");
  s.Observe(AuthResult.NetworkError); Equal(false,s.CanAttempt,"network changes cannot resume invalid credentials");
  s.Start(); Equal(true,s.CanAttempt,"explicit restart releases auth pause");
  s.Stop(); Equal(false,s.CanAttempt,"stop prevents next login");
  s.Observe(AuthResult.Online); Equal(false,s.CanAttempt,"late network result must not undo stop");
  var account=new Account{Username="fixture-username-12345",Password="fixture-secret-12345\"\\\r\n密码"};
  var config=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(EngineConfig.Create(account));
  var form=(Dictionary<string,object>)config["form"];
  Equal(account.Password,Convert.ToString(form["password"]),"special-character password survives engine JSON");
  var settings=(Dictionary<string,object>)config["settings"];
  Equal(true,((Dictionary<string,object>)settings["basic"])["https"],"authentication must use TLS");
  Equal(false,((Dictionary<string,object>)settings["basic"])["skip_cert_verify"],"certificate validation remains on");
  Equal(false,((Dictionary<string,object>)settings["guardian"])["enable"],"one UI attempt cannot start a second permanent monitor");
  Equal(false,((Dictionary<string,object>)settings["log"])["write_file"],"engine does not save raw credential-bearing diagnostic output");
  string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"storage-test-"+Guid.NewGuid().ToString("N"));
  var store=new Store(dir);store.Save(account);Equal(true,store.HasAccount,"saved credential is available");
  Equal(false,Encoding.UTF8.GetString(File.ReadAllBytes(store.FilePath("account.dat"))).Contains("fixture-secret"),"saved credentials must not contain plaintext password");
  Equal(false,Encoding.UTF8.GetString(File.ReadAllBytes(store.FilePath("account.dat"))).Contains("fixture-username"),"saved credentials must not contain plaintext account");
  Equal(account.Password,store.Load().Password,"DPAPI credential round trip");
  File.WriteAllText(store.FilePath("runtime-fixture.json"),"temporary password");File.WriteAllText(store.FilePath("settings-sentinel.json"),"keep");store.RemoveRuntime();
  Equal(false,File.Exists(store.FilePath("runtime-fixture.json")),"stale runtime credentials are cleared");
  Equal(true,File.Exists(store.FilePath("settings-sentinel.json")),"cleanup cannot delete unrelated files");
  store.ClearAccount();Equal(false,store.HasAccount,"clear removes encrypted credential");
  File.WriteAllText(store.FilePath("tampered-engine.exe"),"invalid binary");Equal(false,EngineRunner.Verify(store.FilePath("tampered-engine.exe")),"altered engine is rejected before authentication");
  Directory.Delete(dir,true);
  Console.WriteLine(assertions+" assertions, "+failures+" failures"); return failures==0?0:1;
 }
}


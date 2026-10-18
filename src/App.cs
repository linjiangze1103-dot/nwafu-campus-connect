using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Principal;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace NwafuConnect {
 static class Program {
  public const string Version="1.2.2";
  public static EventWaitHandle ShowRequest;
  [STAThread] static void Main(string[] args) {
   Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
   if(!Environment.Is64BitOperatingSystem){MessageBox.Show("此软件包需要 64 位 Windows 10 或 Windows 11。","系统不支持");return;}
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
   string sid=WindowsIdentity.GetCurrent().User.Value;
   bool first;
   using(var mutex=new Mutex(true,"Local\\NWAFU_CampusConnect_"+sid,out first)) {
    if(!first) {if(Array.IndexOf(args,"--autostart")>=0)return;try{using(var ev=EventWaitHandle.OpenExisting("Local\\NWAFU_CampusConnect_Show_"+sid)){ev.Set();}}catch{}return;}
    using(ShowRequest=new EventWaitHandle(false,EventResetMode.AutoReset,"Local\\NWAFU_CampusConnect_Show_"+sid)) {
     try {
      var store=new Store(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NWAFU-CampusConnect"));
      Application.Run(new MainWindow(store,Array.IndexOf(args,"--autostart")>=0));
     } catch(Exception) {MessageBox.Show("软件未能初始化。请将软件包解压到本机可读写文件夹后重新打开；如仍失败，请联系分享者。","西农校园网助手",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
   }
  }
 }
 public sealed class MainWindow:Form {
  readonly Store store; readonly Preferences prefs; readonly bool startupLaunch;
  readonly MonitorState state=new MonitorState(); readonly List<string> journal=new List<string>();
  readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
  readonly TextBox username=new TextBox(),password=new TextBox();
  readonly Label headline=new Label(),details=new Label(),schedule=new Label(),autoState=new Label(),passwordHint=new Label();
  readonly CheckBox remember=new CheckBox(),showPassword=new CheckBox(),notices=new CheckBox();
  readonly Button begin=new Button(),check=new Button(),stop=new Button(),autoButton=new Button();
  readonly TextBox log=new TextBox();
  readonly Func<CancellationToken,PortalState> queryPortal;
  readonly Func<Account,CancellationToken,AuthResult> authenticate;
  bool networkCheckPending;
  Account account; bool busy,quitting; int generation; CancellationTokenSource cancel;
  DateTime due=DateTime.UtcNow,lastCheck=DateTime.MinValue; string lastStatus="尚未启动"; AuthResult? lastNotice;
  const string RunKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run", RunName="NWAFU-CampusConnect";
  static readonly Color Green=Color.FromArgb(28,105,80),Ink=Color.FromArgb(30,51,47),Muted=Color.FromArgb(94,112,106),Paper=Color.FromArgb(245,248,246);
  public MainWindow(Store store,bool startupLaunch,Func<CancellationToken,PortalState> queryPortal=null,Func<Account,CancellationToken,AuthResult> authenticate=null) {

   this.queryPortal=queryPortal??PortalClient.Query;
   this.authenticate=authenticate??((a,t)=>new EngineRunner().Run(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"engine","bitsrun.exe"),store,a,t));
   this.store=store;this.startupLaunch=startupLaunch;prefs=store.LoadPreferences();store.RemoveRuntime();
   SuspendLayout();
   Text="西农校园网助手"; ClientSize=new Size(800,632); FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;
   StartPosition=FormStartPosition.CenterScreen;BackColor=Paper;ForeColor=Ink;Font=new Font("Microsoft YaHei UI",9F);AutoScaleDimensions=new SizeF(96F,96F);AutoScaleMode=AutoScaleMode.Dpi;
   try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
   MakeUI();ResumeLayout(false);
   try{account=store.Load();if(account!=null){username.Text=account.Username;passwordHint.Text="密码已加密保存，留空可沿用";remember.Checked=true;}}
   catch{account=null;SetStatus("保存的凭据无法读取","请重新填写账号密码；更换 Windows 用户后需要重新保存。",false);}
   notices.Checked=prefs.Notifications;
   showPassword.CheckedChanged+=delegate{password.UseSystemPasswordChar=!showPassword.Checked;};
   notices.CheckedChanged+=delegate{prefs.Notifications=notices.Checked;SavePrefs();};
   begin.Click+=delegate{SaveAndStart();};check.Click+=delegate{ImmediateCheck();};stop.Click+=delegate{StopMonitoring();};
   autoButton.Click+=delegate{ChangeStartup(!StartupEnabled());};
   timer.Interval=500;timer.Tick+=delegate{Tick();};timer.Start();
   NetworkChange.NetworkAddressChanged+=NetworkChanged;
   NetworkChange.NetworkAvailabilityChanged+=AvailabilityChanged;
   Microsoft.Win32.SystemEvents.PowerModeChanged+=PowerChanged;
   if(account!=null && !prefs.Paused) {state.Start();due=DateTime.UtcNow;SetStatus("等待检测","后台自动连接已开启。",false);}
   Shown+=delegate {
    if(startupLaunch && account!=null) Hide();
   };
   FormClosing+=ClosingWindow;FormClosed+=delegate{timer.Stop();NetworkChange.NetworkAddressChanged-=NetworkChanged;NetworkChange.NetworkAvailabilityChanged-=AvailabilityChanged;SystemEvents.PowerModeChanged-=PowerChanged;};
   AddLog("v"+Program.Version+(startupLaunch?" 已通过 Windows 自启启动。":" 已启动。"));AddLog(account==null?"未找到可用账号，请在界面保存账号。":(prefs.Paused?"已保存暂停状态，需手动启动。":"已加载本机账号，后台自动检测已开启。"));UpdateControls();
  }
  void MakeUI() {
   var band=new Panel{Location=new Point(0,0),Size=new Size(800,104),BackColor=Color.White};Controls.Add(band);
   var branding=new PictureBox{Location=new Point(0,-12),Size=new Size(360,128),SizeMode=PictureBoxSizeMode.Zoom,AccessibleName="西北农林科技大学校徽与校名"};
   using(var stream=typeof(MainWindow).Assembly.GetManifestResourceStream("NwafuConnect.crest.png")) {
    if(stream!=null)using(var original=Image.FromStream(stream)){branding.Image=new Bitmap(original);}
   }
   band.Controls.Add(branding);
   var divider=new Panel{Location=new Point(370,26),Size=new Size(1,52),BackColor=Color.FromArgb(220,229,223)};band.Controls.Add(divider);
   LabelAt(band,"西农校园网助手",396,22,370,36,20F);
   LabelAt(band,"便捷连接校园网络  ·  非官方工具",399,65,360,24).ForeColor=Muted;
   var status=Card(this,24,120,752,108);
   var stripe=new Panel{Location=new Point(0,0),Size=new Size(4,108),BackColor=Green};status.Controls.Add(stripe);
   LabelAt(status,"连接状态",20,12,110,20).ForeColor=Muted;
   headline.SetBounds(20,37,265,36);headline.Font=new Font(Font.FontFamily,18F,FontStyle.Bold);headline.Text="尚未启动";headline.ForeColor=Green;status.Controls.Add(headline);
   details.Font=Font;details.SetBounds(310,20,420,42);details.ForeColor=Ink;details.Text="填写账号密码，点击“保存并启动”即可使用。";status.Controls.Add(details);
   schedule.Font=Font;schedule.SetBounds(310,73,420,24);schedule.ForeColor=Muted;status.Controls.Add(schedule);
   LabelAt(status,"NWAFU  /  校园网认证",21,80,265,20).ForeColor=Muted;
   var card=Card(this,24,244,472,264);
   LabelAt(card,"上网账号",20,16,160,27,12F);
   LabelAt(card,"使用学校网页认证账号",20,48,420,24).ForeColor=Muted;
   LabelAt(card,"账号",20,87,48,24);username.SetBounds(76,82,376,28);username.MaxLength=128;username.Font=new Font(Font.FontFamily,11F);card.Controls.Add(username);
   LabelAt(card,"密码",20,129,48,24);password.SetBounds(76,124,298,28);password.UseSystemPasswordChar=true;password.MaxLength=512;password.Font=new Font(Font.FontFamily,11F);card.Controls.Add(password);
   showPassword.Text="显示";showPassword.SetBounds(389,126,64,25);card.Controls.Add(showPassword);
   passwordHint.Font=Font;passwordHint.Text="使用网页认证时输入的同一组账号密码";passwordHint.ForeColor=Muted;passwordHint.SetBounds(76,157,376,22);card.Controls.Add(passwordHint);
   remember.Text="在此电脑加密保存账号密码";remember.Checked=true;remember.SetBounds(76,184,360,25);card.Controls.Add(remember);
   ButtonAt(begin,"保存并启动",20,218,140,34,Green,Color.White);card.Controls.Add(begin);
   ButtonAt(check,"立即检测",170,218,132,34,Color.White,Ink);card.Controls.Add(check);
   ButtonAt(stop,"停止自动连接",312,218,140,34,Color.White,Color.FromArgb(154,63,51));card.Controls.Add(stop);
   var settings=Card(this,512,244,264,264);
   LabelAt(settings,"运行设置",20,16,220,27,12F);
   autoState.Font=Font;autoState.SetBounds(20,53,226,25);settings.Controls.Add(autoState);
   ButtonAt(autoButton,"开启自启",20,83,224,32,Color.White,Ink);settings.Controls.Add(autoButton);
   LabelAt(settings,"仅影响下次登录 Windows",20,122,226,22).ForeColor=Muted;
   var line=new Panel{Location=new Point(20,153),Size=new Size(224,1),BackColor=Color.FromArgb(230,236,232)};settings.Controls.Add(line);
   notices.Text="异常时提醒我";notices.SetBounds(20,166,224,25);settings.Controls.Add(notices);
   LabelAt(settings,"仅在需要处理或持续失败时提醒",20,196,226,22).ForeColor=Muted;
   var clear=new LinkLabel{Font=Font,Text="清除账号与自启",Location=new Point(20,230),Size=new Size(224,24),LinkColor=Muted};clear.LinkClicked+=delegate{ClearAccount();};settings.Controls.Add(clear);
   var activity=Card(this,24,524,752,64);
   LabelAt(activity,"运行记录",16,12,100,22).ForeColor=Muted;
   var copy=new LinkLabel{Font=Font,Text="复制诊断",Location=new Point(16,37),Size=new Size(100,22),LinkColor=Green};copy.LinkClicked+=delegate{CopyDiagnostics();};activity.Controls.Add(copy);
   log.SetBounds(120,10,616,46);log.Multiline=true;log.ReadOnly=true;log.ScrollBars=ScrollBars.Vertical;log.BorderStyle=BorderStyle.None;log.BackColor=Color.White;log.ForeColor=Muted;log.Font=new Font("Microsoft YaHei UI",8.5F);activity.Controls.Add(log);
   LabelAt(this,"关闭窗口后继续运行；再次双击软件即可打开。",24,604,500,22).ForeColor=Muted;
   var help=new LinkLabel{Font=Font,Text="使用说明",Location=new Point(594,604),Size=new Size(85,22),LinkColor=Green};help.LinkClicked+=delegate{ShowHelp();};Controls.Add(help);
   LabelAt(this,"v"+Program.Version,700,604,76,22).ForeColor=Muted;
   username.TabIndex=0;password.TabIndex=1;showPassword.TabIndex=2;remember.TabIndex=3;begin.TabIndex=4;check.TabIndex=5;stop.TabIndex=6;
   AcceptButton=begin;
  }
  static Panel Card(Control parent,int x,int y,int w,int h) {
   var panel=new Panel{Location=new Point(x,y),Size=new Size(w,h),BackColor=Color.White};parent.Controls.Add(panel);return panel;
  }
  // Set the final font BEFORE bounds: legacy WinForms Label rescales its size on font inheritance at high DPI.
  static Label LabelAt(Control parent,string text,int x,int y,int w,int h,float headingSize=0){var l=new Label{Font=headingSize>0?new Font(parent.Font.FontFamily,headingSize,FontStyle.Bold):parent.Font,Text=text,Location=new Point(x,y),Size=new Size(w,h)};parent.Controls.Add(l);return l;}
  void ButtonAt(Button button,string text,int x,int y,int w,int h,Color back,Color fore) {button.Text=text;button.SetBounds(x,y,w,h);button.BackColor=back;button.ForeColor=fore;button.FlatStyle=FlatStyle.Flat;button.FlatAppearance.BorderColor=Color.FromArgb(215,225,218);button.Cursor=Cursors.Hand;button.FlatAppearance.MouseOverBackColor=back==Green?Color.FromArgb(35,120,92):Color.FromArgb(236,244,239);Controls.Add(button);}
  void SavePrefs(){try{store.SavePreferences(prefs);}catch{AddLog("设置未能保存，请检查当前用户的数据目录权限。");}}
  void SaveAndStart() {
   try {
    string user=username.Text.Trim();
    if(String.IsNullOrWhiteSpace(user))throw new ArgumentException("请填写上网账号。");
    string secret=password.Text;
    if(secret.Length==0 && account!=null && user==account.Username)secret=account.Password;
    if(String.IsNullOrEmpty(secret))throw new ArgumentException("请填写上网密码。");
    if(busy){MessageBox.Show(this,"正在完成本次检测，请稍后再保存。也可先点击停止。","稍等片刻");return;}
    account=new Account{Username=user,Password=secret};
    if(remember.Checked)store.Save(account);else{store.ClearAccount();if(StartupEnabled())ChangeStartup(false);}
    password.Clear();passwordHint.Text=remember.Checked?"密码已加密保存，留空可沿用":"密码仅在本次进程内保留";
    generation++;state.Start();prefs.Paused=false;SavePrefs();lastNotice=null;due=DateTime.UtcNow;
    SetStatus("等待检测","自动连接已开启，正在检查当前网络。",false);AddLog("自动连接已开启。");UpdateControls();
   } catch(Exception e){MessageBox.Show(this,e is ArgumentException?e.Message:"账号保存失败，请检查软件数据目录权限。","未能启动",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
  }
  void ImmediateCheck() {
   if(account==null){SaveAndStart();return;}
   if(busy)return;
   if(password.Text.Length>0 || username.Text.Trim()!=account.Username){SaveAndStart();return;}
   generation++;state.Start();prefs.Paused=false;SavePrefs();lastNotice=null;due=DateTime.UtcNow;AddLog("已请求立即检测。");UpdateControls();
  }
  void StopMonitoring() {
   generation++;state.Stop();networkCheckPending=false;prefs.Paused=true;SavePrefs();if(cancel!=null)cancel.Cancel();
   SetStatus("已停止自动连接","不再检测或自动认证，当前已连接的网络不会被注销。",false);AddLog("已停止检测和重连，未更改自启设置。");UpdateControls();
  }
  void Tick() {
   if(Program.ShowRequest!=null && Program.ShowRequest.WaitOne(0)) {Show();WindowState=FormWindowState.Normal;Activate();}
   UpdateControls();
   if(!busy && state.CanAttempt && DateTime.UtcNow>=due)CheckAsync();
  }
  async void CheckAsync() {
   if(busy || !state.CanAttempt || account==null)return;
   busy=true;int run=generation;cancel=new CancellationTokenSource();CancellationToken token=cancel.Token;var savedAccount=account;
   AddLog("开始检测校园网，窗口"+(Visible?"已显示":"已隐藏")+"。");headline.Text="正在检测校园网…";details.Text="先确认校园网环境，再决定是否提交认证。";UpdateControls();
   try {
    PortalState observed=await Task.Run(delegate{return queryPortal(token);},token);
    if(token.IsCancellationRequested || run!=generation)return;
    AuthResult result;
    if(observed==PortalState.Invalid){result=AuthResult.NetworkError;details.Text="未确认西农认证网络，请连接校园 Wi-Fi 或网线。";}
    else if(observed==PortalState.Online)result=AuthResult.Online;
    else {
     headline.Text="正在认证…";details.Text="已确认学校认证入口，正在使用本机保存的账号。";
     result=await Task.Run(delegate{return authenticate(savedAccount,token);},token);
     if(token.IsCancellationRequested || run!=generation)return;
     if(result==AuthResult.Success || result==AuthResult.Online) {
      PortalState after=await Task.Run(delegate{return queryPortal(token);},token);
      if(after!=PortalState.Online)result=AuthResult.NetworkError;
     }
    }
    if(token.IsCancellationRequested || run!=generation)return;
    ApplyResult(result);
   } catch(OperationCanceledException) {}
   catch(InvalidDataException) {if(run==generation){ApplyResult(AuthResult.Rejected);SetStatus("认证引擎校验失败","请重新解压原始软件包，不要使用被替换的引擎文件。",true);}}
   catch(Exception) {if(!token.IsCancellationRequested && run==generation)ApplyResult(AuthResult.NetworkError);}
   finally {busy=false;cancel.Dispose();cancel=null;if(networkCheckPending && state.CanAttempt)due=DateTime.UtcNow;networkCheckPending=false;UpdateControls();}
  }
  void ApplyResult(AuthResult result) {
   state.Observe(result);lastCheck=DateTime.Now;
   int delay=state.Failures==0?60:Policy.RetrySeconds(state.Failures);due=DateTime.UtcNow.AddSeconds(delay);
   bool blocked=state.Blocked;
   if(blocked){prefs.Paused=true;SavePrefs();}
   string title=blocked?"需要处理 · 已暂停":(result==AuthResult.NetworkError?"等待校园网连接":"校园网已认证");
   string message=Policy.Describe(result);
   if(result==AuthResult.NetworkError)message="未确认校园网或服务器暂不可用。请连接 NWAFU Wi-Fi 或校园网线；"+delay+" 秒后重试。";
   SetStatus(title,message,blocked);
   if(lastStatus!=result.ToString() || result==AuthResult.NetworkError){AddLog(message);lastStatus=result.ToString();}
   if(prefs.Notifications && (blocked || (result==AuthResult.NetworkError && state.Failures==10)) && lastNotice!=result) {
    lastNotice=result;BeginInvoke((Action)delegate{if(!quitting)MessageBox.Show(message+"\r\n\r\n再次双击软件可打开控制界面。","西农校园网助手 · 需要处理",MessageBoxButtons.OK,MessageBoxIcon.Information);});
   }
  }
  void SetStatus(string title,string message,bool error){headline.Text=title;headline.ForeColor=error?Color.FromArgb(161,73,46):Green;details.Text=message;}
  void UpdateControls() {
   bool active=state.Enabled;
   begin.Enabled=!busy;check.Enabled=!busy;stop.Enabled=active || busy;
   bool auto=StartupEnabled();autoState.Text="随 Windows 启动："+(auto?"已开启":"已关闭");autoButton.Text=auto?"关闭自启":"开启自启";
   string last=lastCheck==DateTime.MinValue?"尚无检测记录":"上次检测 "+lastCheck.ToString("HH:mm:ss");
   schedule.Text=last+(busy?"  ·  检测中":(state.CanAttempt?"  ·  下次检测约 "+Math.Max(0,(int)(due-DateTime.UtcNow).TotalSeconds)+" 秒后":(state.Blocked?"  ·  已暂停重试":"  ·  自动连接已停止")));
  }
  bool StartupEnabled(){try{using(var key=Registry.CurrentUser.OpenSubKey(RunKey)){return key!=null && key.GetValue(RunName)!=null;}}catch{return false;}}
  void ChangeStartup(bool enabled) {
   try {
    if(enabled && !store.HasAccount){MessageBox.Show(this,"请先勾选“在此电脑加密保存账号密码”，然后保存并启动。","开启自启前请先保存账号");return;}
    using(var key=Registry.CurrentUser.CreateSubKey(RunKey)){if(enabled)key.SetValue(RunName,"\""+Application.ExecutablePath+"\" --autostart",RegistryValueKind.String);else key.DeleteValue(RunName,false);}
    AddLog(enabled?"已开启当前用户登录 Windows 后自启。":"已关闭自启，本次自动连接状态不变。");UpdateControls();
   }catch{MessageBox.Show(this,"无法修改自启设置，请检查当前用户权限。","自启设置失败");}
  }
  void ClearAccount() {
   if(MessageBox.Show(this,"将停止自动连接、关闭自启，并清除本机保存的账号密码。当前网络不会被注销。是否继续？","清除本机配置",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
   StopMonitoring();ChangeStartup(false);store.ClearAccount();account=null;username.Clear();password.Clear();passwordHint.Text="使用网页认证时输入的同一组账号密码";AddLog("已清除本机保存的账号密码。");
  }
  void AddLog(string message) {
   string line=DateTime.Now.ToString("MM-dd HH:mm:ss")+"  "+message;journal.Add(line);if(journal.Count>80)journal.RemoveAt(0);
   log.Lines=journal.ToArray();log.SelectionStart=log.TextLength;log.ScrollToCaret();
   try{File.WriteAllLines(store.FilePath("diagnostic.log"),journal.ToArray(),new UTF8Encoding(false));}catch{}
  }
  void CopyDiagnostics() {
   string text="西农校园网助手 "+Program.Version+"\r\nWindows 64位："+Environment.Is64BitOperatingSystem+"\r\n自启："+StartupEnabled()+"\r\n自动连接："+state.CanAttempt+"\r\n状态："+headline.Text+"\r\n\r\n"+String.Join("\r\n",journal.ToArray());
   try{Clipboard.SetText(text);MessageBox.Show(this,"已复制诊断信息，不包含账号、密码、IP 地址或认证令牌。","复制完成");}catch{MessageBox.Show(this,"剪贴板暂不可用，请稍后重试。","复制失败");}
  }
  void ShowHelp() {
   MessageBox.Show(this,"1. 先连接 NWAFU Wi-Fi 或校园网线，填写账号密码并保存启动。\r\n2. 关闭窗口后仍继续运行；再次双击软件打开同一界面。\r\n3. 停止自动连接不会注销当前网络；关闭自启不停止本次运行。\r\n4. 密码或认证被拒绝时暂停重试，处理后点击启动或立即检测。\r\n5. 开启自启前请把软件解压到固定位置，之后不要移动文件夹。\r\n6. 忘记自启路径或换电脑时，重新开启自启即可。\r\n\r\n账号密码仅用于 https://portal.nwafu.edu.cn。保存时由 Windows 当前用户加密；认证时有短暂、受权限保护的明文临时配置，认证结束即删除。日志只记录概括状态。\r\n\r\n本软件非学校官方产品；认证状态不代表已验证外网访问。","使用说明",MessageBoxButtons.OK,MessageBoxIcon.Information);
  }
  void NetworkChanged(object sender,EventArgs e){ScheduleNetworkCheck();}
  void AvailabilityChanged(object sender,NetworkAvailabilityEventArgs e){ScheduleNetworkCheck();}
  void PowerChanged(object sender,PowerModeChangedEventArgs e){if(e.Mode==PowerModes.Resume)ScheduleNetworkCheck();}
  void ScheduleNetworkCheck(){if(IsHandleCreated && !quitting)try{BeginInvoke((Action)delegate{if(state.CanAttempt){state.Failures=0;if(busy)networkCheckPending=true;else due=DateTime.UtcNow;}});}catch{}}
  void ClosingWindow(object sender,FormClosingEventArgs e) {
   if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();return;}
   quitting=true;generation++;state.Stop();if(cancel!=null)cancel.Cancel();
  }
 }
}








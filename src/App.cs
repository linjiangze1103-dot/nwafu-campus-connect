using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
namespace NwafuConnect {
 public sealed class MainWindow:Form {
  readonly Store store;readonly ConnectionController core;readonly IStartupManager startup;readonly DiagnosticLog diagnostics;
  readonly Timer timer=new Timer();readonly List<string> journal=new List<string>();
  readonly TextBox username=new TextBox(),password=new TextBox(),log=new TextBox();
  readonly Label headline=new Label(),details=new Label(),schedule=new Label(),autoState=new Label(),passwordHint=new Label();
  readonly CheckBox remember=new CheckBox(),showPassword=new CheckBox(),notices=new CheckBox();
  readonly Button begin=new Button(),check=new Button(),stop=new Button(),autoButton=new Button();
  StartupStatus startupState;bool quitting;string lastStage;
  static readonly Color Green=Color.FromArgb(28,105,80),Ink=Color.FromArgb(30,51,47),Muted=Color.FromArgb(94,112,106),Paper=Color.FromArgb(245,248,246);
  public MainWindow(Store store,ConnectionController controller,IStartupManager startup,DiagnosticLog diagnostics=null){
   this.store=store;core=controller;this.startup=startup;this.diagnostics=diagnostics;startupState=startup.Read();
   SuspendLayout();Text="西农校园网助手";ClientSize=new Size(800,632);FormBorderStyle=FormBorderStyle.FixedSingle;MaximizeBox=false;
   StartPosition=FormStartPosition.CenterScreen;BackColor=Paper;ForeColor=Ink;Font=new Font("Microsoft YaHei UI",9F);AutoScaleDimensions=new SizeF(96F,96F);AutoScaleMode=AutoScaleMode.Dpi;
   try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
   MakeUI();ResumeLayout(false);var account=core.CurrentAccount;if(account!=null){username.Text=account.Username;remember.Checked=store.HasAccount;passwordHint.Text="密码已加密保存，留空可沿用";}
   notices.Checked=core.Notifications;notices.CheckedChanged+=delegate{core.SetNotifications(notices.Checked);};
   showPassword.CheckedChanged+=delegate{password.UseSystemPasswordChar=!showPassword.Checked;};
   begin.Click+=delegate{SaveAndStart();};check.Click+=delegate{if(password.Text.Length>0 || core.CurrentAccount==null || core.CurrentAccount.Username!=username.Text.Trim())SaveAndStart();else core.CheckNow();};
   stop.Click+=delegate{core.Stop();UpdateControls();};autoButton.Click+=delegate{ChangeStartup(!startupState.Enabled);};
   FormClosing+=delegate(object s,FormClosingEventArgs e){if(!quitting && e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}};
   FormClosed+=delegate{timer.Stop();};timer.Interval=250;timer.Tick+=delegate{UpdateControls();};timer.Start();UpdateControls();
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
  void SaveAndStart(){try{
   string user=username.Text.Trim();var prior=core.CurrentAccount;string secret=password.Text;
   if(secret.Length==0 && prior!=null && user==prior.Username)secret=prior.Password;
   if(String.IsNullOrWhiteSpace(user)||String.IsNullOrEmpty(secret))throw new ArgumentException("请填写账号和密码。");
   var account=new Account{Username=user,Password=secret};
   if(remember.Checked)store.Save(account);else{store.ClearAccount();startupState=startup.SetEnabled(false);}
   password.Clear();passwordHint.Text=remember.Checked?"密码已加密保存，留空可沿用":"密码仅在本次进程内保留";core.Start(account);UpdateControls();
  }catch(Exception e){MessageBox.Show(this,e is ArgumentException?e.Message:"保存失败，请检查数据目录权限。","未能保存");}}
  void ChangeStartup(bool enabled){if(enabled&&!store.HasAccount){MessageBox.Show(this,"请先保存账号密码。");return;}startupState=startup.SetEnabled(enabled);if(startupState.ErrorCode!=null)MessageBox.Show(this,"自启设置未完全成功："+startupState.ErrorCode,"自启设置");UpdateControls();}
  void UpdateControls(){
   var s=core.Snapshot;string title,body;
   switch(s.Stage){
    case "online":title=s.InternetVerified?"已连接校园网":"校园网已认证";body=s.InternetVerified?"已验证外网连通。":"学校认证状态正常。";break;
    case "internet":title="校园网已认证";body="外网检测暂未通过，正在自动复查。";break;
    case "auth":title="正在认证…";body="正在使用本机账号完成学校认证。";break;
    case "probe":title="正在检测校园网…";body="后台正在确认学校认证入口。";break;
    case "blocked":title="需要处理 · 已暂停";body=Policy.Describe(s.Result);break;
    case "unknown":title="认证结果无法确认";body="已暂停重复认证，请检查网页状态后重新启动。";break;
    case "engine":title="认证引擎需要检查";body="引擎校验或退出异常，请重新安装软件包。";break;
    case "credentials":title="请重新保存账号";body="本机保存的凭据无法读取。";break;
    case "paused":title="已停止自动连接";body="已保存暂停状态；点击保存并启动可恢复。";break;
    default:title="等待校园网连接";body="连接校园 Wi-Fi 或网线后，后台自动检测。";break;
   }
   if(core.CurrentAccount==null){title="请填写上网账号";body="保存账号后即可自动连接。";}
   headline.Text=title;headline.ForeColor=s.Blocked?Color.FromArgb(161,73,46):Green;details.Text=body;
   schedule.Text=s.SettingsFailed?"设置保存失败，重启前请检查权限":(diagnostics!=null && diagnostics.WriteFailed?"日志写入失败，可复制内存诊断":(s.Busy?"后台正在处理":(s.Enabled&&!s.Blocked?"自动连接已开启":"自动连接已暂停")));
   begin.Enabled=!s.Busy;check.Enabled=!s.Busy;stop.Enabled=s.Enabled||s.Busy;
   autoState.Text="随 Windows 启动："+(startupState.Enabled?(startupState.Fast?"快速模式":"普通模式"):"已关闭");autoButton.Text=startupState.Enabled?"关闭自启":"开启自启";
   if(lastStage!=s.Stage){lastStage=s.Stage;AddLog(body);}
  }
  void AddLog(string text){journal.Add(DateTime.Now.ToString("HH:mm:ss")+"  "+text);if(journal.Count>80)journal.RemoveAt(0);log.Lines=journal.ToArray();log.SelectionStart=log.TextLength;log.ScrollToCaret();}
  void ClearAccount(){if(MessageBox.Show(this,"停止连接、关闭自启并清除本机账号？","清除配置",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;core.Stop();startupState=startup.SetEnabled(false);store.ClearAccount();username.Clear();password.Clear();AddLog("本机保存的账号已清除。");}
  void CopyDiagnostics(){try{Clipboard.SetText("西农校园网助手 "+Program.Version+"\r\n"+(diagnostics==null?String.Join("\r\n",journal):String.Join("\r\n",diagnostics.Snapshot())));MessageBox.Show(this,"已复制脱敏诊断。");}catch{MessageBox.Show(this,"剪贴板暂不可用。");}}
  void ShowHelp(){MessageBox.Show(this,"保存账号后自动认证；关闭窗口继续运行，无托盘。\r\n再次双击打开界面；停止状态会保留到下次启动。\r\n关闭自启只影响以后登录，不停止当前连接。\r\n快速自启在登录 Windows 后运行，仍需网卡就绪。\r\n账号仅在本机按 Windows 用户加密保存。\r\n本软件为非官方工具。","使用说明");}
  public void Shutdown(){quitting=true;timer.Stop();Close();}
  protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
 }
}

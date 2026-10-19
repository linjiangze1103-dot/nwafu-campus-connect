# 西农校园网助手快速连接 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 登录 Windows 且校园网络就绪后，无需打开应用或手动检测，正常情况下 5 秒内验证可上网。

**Architecture:** 一个当前用户后台进程持有独立连接工作循环，窗口按需创建。使用现有 BitSrunLoginGo 认证算法，统一网络路径和截止时间；通过稳定安装目录和统一自启管理完成旧版本迁移。

**Tech Stack:** Windows x64、系统 .NET Framework、C# 5、WinForms、Task Scheduler COM、DPAPI、固定版本 BitSrunLoginGo v1.6.8；PowerShell 5.1 用于构建和测试，不作为核心运行依赖。

**Spec:** `docs/superpowers/specs/2026-10-19-fast-connect-design.md`，用户已于 2026-10-19 确认。

## Global Constraints

- 当前阶段为实施计划审阅，应用代码尚未改动。所有步骤完成并验证后才勾选，不预先填写通过结果。
- 目标版本 v1.3.0；真机验收前标记测试版本。正常目标为 `I - T0 ≤ 5 秒`，`T0 = max(L, N)`；L 为用户登录时间，N 为校园接口地址与候选路由就绪的最早可观测时间。
- “停止”跨重启保持；“关闭自启”不停止当前连接；关窗口继续运行；无托盘；账号数据不随包发布。
- 核心不依赖 GUI 消息泵；网络事件合并窗口 150 毫秒；同一时间最多一个认证任务。
- 快速恢复窗口 30 秒；只读失败间隔 0.5、1、2 秒，随后保持 2 秒；普通退避 5、10、30、60 秒；两次认证提交至少相隔 2 秒；正常在线 60 秒检查一次。
- 学校探测整次预算快速 1.5 秒、普通 3 秒；认证快路径请求 2 秒／进程 8 秒，慢路径请求 4 秒／进程 15 秒。超过 5 秒的成功也不能算五秒验收通过。
- 固定学校 HTTPS、证书校验、禁止状态探测重定向；不修改系统代理；只清理认证子进程的代理变量。
- 保留引擎哈希校验与现有 DPAPI 数据；兼容用户原暂停和自启意愿；不依赖安装系统服务或管理员权限。
- PS1 保持 UTF-8 BOM；源码与构建产物版本一致；源码、许可证、上游对应源码随发布包提供。
- 自动化测试只使用隔离存储和模拟认证，不把测试账号发往学校，不触碰真实自启项；需要真实用户验证的步骤单列。
- 未经用户安排不重启、主动断网或注销；冷启动验收需用户完成重启动作后继续读取日志。

## Review Focus

1. 测试原来靠关闭 UI 定时器避免联网；解耦后该措施失效：Task 3/4 必须让所有测试显式注入假传输。
2. 引擎已经提交成功但响应丢失：Task 2/3 先查状态再决定重发，防止重复认证。
3. 软件在压缩包内运行、旧路径已移动或程序仍被占用：Task 5 拒绝不完整源、受控切换并可回滚。
4. 自启写入部分成功、关闭开关后残留任务：Task 5 入口检查独立启用标志，读回失败不显示“快速自启已开启”。
5. 网卡就绪事件漏报或日志时间无法对齐：Task 3/6 用初始快照和补充检查恢复；Task 7 标记测量不足，不伪造五秒达标。

## 文件与依赖映射

新增：`src/Diagnostics.cs`（时钟、非阻塞日志）；`src/Transport.cs`（状态、认证、外网适配）；`src/ConnectionController.cs`（调度与状态）；`src/NetworkObserver.cs`（网络快照）；`src/AppHost.cs`（进程生命周期）；`src/StartupManager.cs`（自启）；`src/Installer.cs`（独立安装升级入口）。

修改：`src/Core.cs` 保留账号、存储、策略与配置，移出网络和进程执行；`src/App.cs` 只保留界面；`src/EarlyStart.cs` 退出新包的构建，旧入口仅由迁移代码识别；`Build.ps1`、`src/AssemblyInfo.cs`、现有测试脚本、发布脚本、CI 与 README 随所属任务同步调整。

新增测试：`tests/DiagnosticsTests.cs`、`TransportTests.cs`、`ControllerTests.cs`、`HostTests.cs`、`StartupTests.cs`、`InstallerTests.cs`、`BenchmarkTests.cs`；共享 `tests/Fakes.cs`、`tests/Run-FastConnectTests.ps1`。测试编译的临时产物置于已忽略的 build 目录。

依赖顺序：1 → 2 → 3 → 4 → 5 → 6 → 7。任务间提交和验证，不把未完成的新核心部署到本机日常自启。

## Task 1：计时、诊断与测试隔离基础

**Files:** 新建 `src/Diagnostics.cs`、`tests/DiagnosticsTests.cs`、`tests/Fakes.cs`、`tests/Run-FastConnectTests.ps1`；修改 `src/Core.cs`、`Build.ps1`。

**Interfaces:**
- `IClock { long Milliseconds { get; } DateTime UtcNow { get; } }`，`SystemClock` 用 Stopwatch；`FakeClock` 供测试推进时间。
- `IDiagnostics.Record(string stage, long generation, long elapsedMs, string outcome)`；不接受任意异常文本或请求正文。
- `DiagnosticLog(Store store, IClock clock, string version, string source)`，`bool WriteFailed`、`string[] Snapshot()`、`Dispose()`。
- `Store.SavePreferences(Preferences value)` 改为同目录临时文件加原子替换；旧 JSON 缺少新字段仍可读取。

- [ ] 添加 `LogsNeverContainCredentialFixture`、`SlowDiskDoesNotBlockCaller`、`RotationRetainsCurrentRun`、`SettingsInterruptedWriteRetainsOldValue`。断言记录入口不等待磁盘写入；1 MiB 轮转、最多 3 个文件；日志可见当前运行 ID；拒绝写入时内存记录仍可用。
- [ ] 创建测试运行脚本 `Run-FastConnectTests.ps1 -Suite Diagnostics`，编译失败或断言失败退出非零；运行确认新接口缺失导致失败。
- [ ] 实现上面接口：单个后台日志写入者、有界队列 256 条、内存最近 80 条；退出最多等待 1 秒；目录写入失败不阻塞核心。原子保存失败必须抛给调用者，不能虚报已保存。
- [ ] 运行 `powershell.exe -NoProfile -File tests/Run-FastConnectTests.ps1 -Suite Diagnostics` 和原 `tests/Run-Tests.ps1`；期望零失败且凭据兼容测试通过。
- [ ] 审查 diff 后提交 `refactor: add bounded diagnostics and atomic preferences`。

## Task 2：认证与探测适配器的截止时间

**Files:** 新建 `src/Transport.cs`、`tests/TransportTests.cs`；修改 `src/Core.cs`、`tests/Tests.cs`，为测试准备挂起／长输出的模拟子进程。

**Interfaces:**
- `IConnectionTransport.QueryAsync(TimeSpan budget, CancellationToken token): Task<PortalState>`。
- `IConnectionTransport.AuthenticateAsync(Account account, bool slowPath, CancellationToken token): Task<AuthResult>`。
- `IConnectionTransport.CheckInternetAsync(CancellationToken token): Task<bool>`。
- `ConnectionTransport(string enginePath, Store store, IDiagnostics log)` 实现接口，使用内部可注入 HTTP/子进程适配，默认绑定真实实现；测试绑定假实现。
- `EngineConfig.Create(Account account, int requestTimeoutSeconds)`；保留现有单参数调用兼容层直到测试迁移完成。

- [ ] 添加 `QueryBudgetIncludesDnsAndBody`、`CancelledDnsDoesNotMultiplyOutstandingLookups`、`HungEngineReapedWithinBudget`、`OutputReaderCannotHangAfterExit`、`ProxyEnvironmentRemovedOnlyForChild`、`InvalidTlsNeverAuthenticates`、`UnknownOutputIsNotCredentialRejection`、`InternetProbeRaceCancelsLoser`。断言预算使用同一截止时间；DNS 未结束时复用任务，不产生无限新解析；超时后返回明确结果而非无限 WaitForExit。
- [ ] 运行 `powershell.exe -NoProfile -File tests/Run-FastConnectTests.ps1 -Suite Transport`，确认旧路径的超时／分类测试失败。
- [ ] 移出 PortalClient/EngineRunner，按规格实现 1500/3000 毫秒请求截止、2000/4000 毫秒引擎请求、8000/15000 毫秒进程预算。终止后回收等待最多 1 秒；无法确认进程退出时报告不可继续认证状态，保留句柄管理，不能启动第二个引擎。
- [ ] 引擎环境移除 `HTTP_PROXY`、`HTTPS_PROXY`、`ALL_PROXY`、`NO_PROXY`（大小写均处理）；不改父环境。读取原始输出只用于内存分类，不写入诊断。明确 `login failed with error: E...` 仍视为服务端拒绝，空输出／格式不识别返回 Unknown。
- [ ] 实现两个既定 HTTPS 外网端点、1 秒整体验证预算、4096 字符上限、HTTP 200 与 User-agent 内容校验；任一成功取消另一个。保持引擎 SHA256 校验及所有正常/异常退出的临时凭据清理。
- [ ] 运行 Transport 和原 Core 套件，期望零失败；再次验证特殊字符密码 JSON 不变。提交 `fix: bound authentication and probe lifetimes`。

## Task 3：独立后台状态机与网络事件

**Files:** 新建 `src/ConnectionController.cs`、`src/NetworkObserver.cs`、`tests/ControllerTests.cs`；修改 `src/Core.cs` 中旧重试规则调用点。

**Interfaces:**
- `NetworkSnapshot { string Fingerprint; bool CandidateReady; long ObservedMs; }`，Fingerprint 仅存内存，日志只记录代次。
- `INetworkObserver { event Action<NetworkSnapshot> Changed; NetworkSnapshot Read(); void Start(); void Dispose(); }`。
- `ConnectionSnapshot { long Generation; string Stage; bool Enabled; bool Blocked; bool Busy; bool InternetVerified; long NextDueMs; AuthResult Result; }`，发布后不可变。
- `ConnectionController(Store store, IConnectionTransport transport, INetworkObserver network, IClock clock, IDiagnostics log)`；`Start(Account account)`、`Stop()`、`CheckNow()`、`Resume()`、`Dispose()`；`ConnectionSnapshot Snapshot { get; }`、`event Action<ConnectionSnapshot> Changed`。

- [ ] 添加 `AuthenticatesWithoutMessagePump`、`NetworkChangeWakesBackoff`、`EventStormDoesNotDuplicateLogin`、`NoEventStillFindsReadyNetwork`、`LateSuccessCannotUndoStop`、`PausedRestartNeverProbes`、`UncertainLoginQueriesBeforeRetry`、`UnrelatedAdapterDoesNotRestartLogin`。用计数断言最大并发认证为 1；用 FakeClock 验证 150 毫秒合并、30 秒窗口和提交间隔至少 2000 毫秒。
- [ ] 运行 Controller 套件并确认新行为尚未实现。
- [ ] 实现线程安全命令队列与可唤醒后台循环；先订阅后读取快照；时钟只用于相对调度。明确账户为空、暂停、可尝试、认证中、在线、账号拒绝和未知故障状态。初始化完成立刻首探测，不等 GUI 定时器。
- [ ] 网络指纹由实际接口状态、有效地址及到学校的候选路由组成；本地候选状态不构成认证授权。网络变化废弃旧只读结果；在途认证完成后仅补一次状态检查，路由丢失才取消认证。循环定时读取快照作为事件漏报补充，不依据 Internet 可用标记拦截认证。
- [ ] 落实全部精确重试值；首次快路径网络失败后允许一次慢路径，再退避。Unknown 连续 3 轮且状态复核仍无法确定时停止主动认证并显示“认证结果无法确认”，保留人工恢复入口；不误标密码错误。明确拒绝立刻持久暂停。
- [ ] Stop 立即取消并禁止新提交；持久化失败时本次仍保持停止、界面明确提示重启后状态可能未保存，不能声称跨重启保证已生效。外网验证失败只补测外网，按 1、2、5 秒间隔，之后维持 5 秒，不重复登录。
- [ ] 运行 Controller/Transport/Core 套件；验证每条状态转换及取消竞态。提交 `refactor: run connection scheduling independently of the UI`。

## Task 4：按需界面与单实例宿主

**Files:** 新建 `src/AppHost.cs`、`tests/HostTests.cs`；修改 `src/App.cs`、`tests/UiTests.cs`、`tests/HiddenTests.cs`、`tests/LayoutTests.cs`、所有对应 Run 脚本和 `Build.ps1`。

**Interfaces:**
- `AppHost(Store store, ConnectionController controller, bool autoStart)` 继承 ApplicationContext；`ShowWindow()`、`RequestShutdown()`。
- `MainWindow(Store store, ConnectionController controller, IStartupManager startup)`，IStartupManager 在 Task 5 定义；本任务先使用测试实现及保留旧注册表行为的临时适配。
- Program 入口拥有 Mutex、显示事件、退出事件；控制器 Changed 只异步投递 UI，后台不调用同步 Invoke。

- [ ] 修改全部 UI 测试注入 FakeTransport/FakeNetwork/FakeStartup；移除“禁用窗口 Timer 就不会联网”的假设。添加 `AutostartCreatesNoForm`、`BlockedUiDoesNotBlockController`、`SecondLaunchDuringInitializationShowsOnce`、`CloseKeepsMonitoring`、`StopPersistsAfterHostRestart`。
- [ ] 运行 Host、UI、Hidden 套件，确认旧宿主创建窗口及 UI 耦合测试失败。
- [ ] 将 Program/生命周期迁到 AppHost；账户与控制器先初始化，启动不加载图形资源。仅第一次显式 ShowWindow 时调用 GUI 资源构造；无账户的静默自启只记录缺失，不弹阻塞对话框。
- [ ] 界面按钮改为发送核心命令；保留布局尺寸、校徽、文字和停止／自启语义；删除 CheckAsync、网络订阅、认证状态管理等旧窗口职责。UI Timer 仅可用于刷新显示，不执行网络工作。
- [ ] 创建显示事件后启动接收；重复手动实例等待事件就绪有界重试（总计 2 秒），自动实例始终静默退出。新增同用户退出事件为后续受控升级提供入口，不将其公开成绕过用户 Stop 语义的普通界面操作。
- [ ] 运行 `tests/Run-UiTests.ps1`、`Run-HiddenTests.ps1`、`Run-LayoutTests.ps1` 及 Host/Controller 套件；确认界面布局无新增重叠、真实网络调用数为 0。提交 `refactor: create the control window only when requested`。

## Task 5：稳定安装、自启统一与可回滚升级

**Files:** 新建 `src/StartupManager.cs`、`src/Installer.cs`、`tests/StartupTests.cs`、`tests/InstallerTests.cs`；修改 `Build.ps1`、`src/AppHost.cs`、`src/App.cs`、`scripts/Enable-FastStartup.ps1`、`Disable-FastStartup.ps1`。

**Interfaces:**
- `StartupStatus { bool Enabled; bool Fast; bool PathVerified; string ErrorCode; }`。
- `IStartupManager.Read(): StartupStatus`、`IStartupManager.SetEnabled(bool enabled): StartupStatus`；`StartupManager(string installedExe, Store store)`。
- `Installer.Install(string sourceDirectory, string targetDirectory): InstallResult`；`InstallResult { bool Success; bool RolledBack; string ErrorCode; }`，独立 `安装或更新.exe` 的 Main 调用此接口。
- 自启标志置 `HKCU\Software\NWAFU-CampusConnect\AutoStartEnabled`；缺失时从已知旧 Run 项迁移一次，关闭优先。任务名沿用 `NWAFU-CampusConnect-EarlyStart` 便于收敛旧安装。

- [ ] 测试 `PartialRegistrationNeverReportsFast`、`DisabledMarkerBlocksStaleTask`、`UpgradePreservesPausedAndCredentials`、`BusyOldProcessRequiresConfirmedExit`、`IncompleteArchiveDoesNotTouchInstallation`、`RollbackRestoresExactStartupState`、`MovedOldPathIsNotBroadlyDeleted`。全部使用临时目标目录和模拟任务／注册表接口。
- [ ] 运行 Startup/Installer 套件，确认缺失迁移／回滚实现导致失败。
- [ ] 使用 Task Scheduler COM 在当前交互用户、Limited 权限、登录触发、无额外延迟、电池允许运行条件下注册主程序 `--autostart`，维护 Run 回退并读回。不开启“必须有网络才运行”的任务条件。入口只在 AutoStartEnabled 生效且账户有效时启用核心，暂停状态仍优先。
- [ ] 实现稳定目标 `%LOCALAPPDATA%\Programs\NWAFU-CampusConnect`。安装器先校验完整清单和引擎哈希，备份程序及原启动配置；所有删除／移动先验证绝对路径属于本应用目标或备份目录。
- [ ] 新版通过同用户退出事件结束旧新版宿主；旧 1.2.x 没有事件时严格核对完整可执行路径、产品身份和匹配的子进程，进行有界退出后才能替换。禁止模糊名称批量结束；未确认退出则回滚且提示，不强行并发新实例。
- [ ] 启动新版本并通过本次升级随机 nonce 的受限目录就绪记录确认版本，最多等待 15 秒。就绪只代表核心初始化成功，不以外网可用决定安装成功。失败恢复旧文件和完全一致的自启意愿；成功才清理本项目已知旧入口。
- [ ] 首次解压启动时提供固定目录安装入口；保存并开启自启自动完成稳定部署，不要求手动运行脚本。旧脚本仅兼容调用新入口；新主路径移除 EarlyStart.exe 构建依赖。迁移保留 DPAPI 文件原字节和 Paused。
- [ ] 运行 Startup/Installer、Host/UI 套件；在隔离安装目录验证中文空格路径和回滚。提交 `feat: install stable paths and manage fast startup in the app`。

## Task 6：版本、基准、发布包和观测工具

**Files:** 修改 `src/AssemblyInfo.cs`、`src/AppHost.cs`、`scripts/Prepare-Release.ps1`、README、`.github/workflows/windows.yml`、`.gitignore`；新增 `tests/BenchmarkTests.cs`、`scripts/Collect-StartupEvidence.ps1`。

**Interfaces:**
- `Collect-StartupEvidence.ps1 -OutputDirectory <path>`：只读本软件阶段日志和当前会话相关系统时间，输出脱敏 JSON/Markdown；字段含 L/N/P/Q/A/C/I、时间依据、误差标记和 MissingEvidence，不输出账号、IP、用户路径或系统事件原文。
- `Run-FastConnectTests.ps1 -Suite All` 运行所有非交互套件；UI 验证独立，全部测试存储均隔离。

- [ ] 编写基准：同样网络响应延迟下比较旧版首次调度与新核心；新版空闲网络事件到探测开始不超过 500 毫秒；明确这是模拟时延。添加 `MissingNetworkTimestampIsNotPass`、`OldRunCannotMatchCurrentBoot`、`EvidenceContainsNoIdentifiers`。
- [ ] 编写证据收集工具：优先系统登录／网络事件，记录事件含义和精度；无法确定最早地址路由就绪时输出证据不足，不退回用首次成功请求充当 N。需要提前启用系统事件记录时显示缺少何种证据，先安排观测再进入重启验收。
- [ ] 版本统一为 1.3.0，加入“待真机验收”的发布说明；新包含安装器、主程序、引擎、源码、许可证和哈希清单。打包仅允许清单中的构建文件；禁止复制 AppData、account.dat、settings.json、runtime JSON、日志或测试存储。
- [ ] 更新 CI：Windows PowerShell 构建、Core、Diagnostics、Transport、Controller、Host 非交互路径、Startup/Installer 模拟测试；需要桌面的实际 UI/DPI 验证在本机运行，不用 CI 无桌面结果冒充 UI 通过。
- [ ] 运行 All、原 Core、UI、Hidden、Layout；生成测试 ZIP、验证哈希与源码可重新构建；清单扫描确认不含本机凭据或日志。提交 `build: package fast-connect candidate with startup evidence`。

## Task 7：本机升级与实际五秒验收

**Files:** 新增 `docs/validation/1.3.0-acceptance.md`（仅写脱敏汇总）；本机完整日志留在安装数据目录或工作区隔离证据目录，不提交 Git。

**Interfaces:** 消费 Task 5 安装器、Task 6 证据工具；不新建认证实现。

- [ ] 保存当前版本与自启配置备份，使用新安装器执行一次本机升级；核验真实用户进程路径、版本、启动任务、自启开关、凭据可读与暂停意愿。不得只根据构建目录文件判断部署成功。
- [ ] 先完成非中断验证：新后台自动运行、关闭窗口不停止、重复打开同一实例、诊断来自新运行 ID。保留当前已认证网络，不主动注销以制造测试样本。
- [ ] 与用户安排重启/网络恢复测试：用户执行至少 3 次系统“重启”及 2 次网络恢复；每次无需打开软件或点检测。记录开始前是否已在线，至少 1 个有效样本需要真实重新认证。
- [ ] 用证据工具计算 `I - max(L,N)` 和四段耗时。超过 5 秒且不能用故障证据归因的样本判未达标；证据不全或全是已在线样本判验收不足。结果按样本逐条保留，不能只报告最快一次。
- [ ] 未达标则按耗时最长的阶段修复并重跑受影响测试及真机验证；不把等待用户重启的测试包称为正式达标版。涉及改变用户级架构或替换认证算法时先报告证据再确认方向。
- [ ] 达标后更新验收摘要和 README，重新生成校验一致的软件包；给用户本地下载链接。发布 GitHub 时保持源码、版本标签、发布包完全对应，并依据用户既有发布授权和实际验证情况说明状态。
- [ ] 提交 `docs: record measured fast-connect acceptance`，只记录实际结果；没有完成的重启样本保持未勾选。

## 自审与执行建议

已逐项映射设计中的后台独立性、请求预算、网络事件、账号暂停、自启、安装迁移、日志、五秒定义和真机门槛。Task 1—6 可以在不中断用户网络的情况下准备完整测试包；Task 7 的重启采样需要用户操作，不能由离线测试替代。

推荐由当前会话连续执行（Native）：状态机、界面、迁移共享紧密的接口，单一执行者更易保持兼容，逐任务测试和提交用于隔离问题。完整改造后进行一次独立代码审查，再进入真机验收；这不替代任何功能测试。

待用户审阅本计划并选择执行方式。设计已确认，实施方式确认后不再为上述范围内的普通代码、测试和打包选择反复询问。

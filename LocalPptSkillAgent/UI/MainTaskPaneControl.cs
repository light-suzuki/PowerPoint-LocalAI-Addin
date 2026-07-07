using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows.Forms;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Services;
using LocalPptSkillAgent.Skills;
using LocalPptSkillAgent.Utils;
using PowerPoint = Microsoft.Office.Interop.PowerPoint;

namespace LocalPptSkillAgent.UI
{
    public class MainTaskPaneControl : UserControl
    {
        private readonly PowerPointService _powerPoint;
        private readonly SettingsService _settingsService = new SettingsService();
        private readonly ErrorMessageService _errors = new ErrorMessageService();
        private readonly ContextBudgetService _budget = new ContextBudgetService();
        private readonly SelectorParser _selectors = new SelectorParser();
        private readonly LlmRouterService _routerService;
        private readonly ConversationService _conversation;
        private readonly LmStudioClient _lmStudio;
        private readonly SkillRouter _router;
        private readonly FactCheckService _factCheck;
        private readonly ActionExecutor _executor;
        private readonly RollbackManager _rollback;
        private readonly ChatSessionManager _chats = new ChatSessionManager();
        private readonly ChatControl _chat = new ChatControl();
        private readonly ContextControl _context = new ContextControl();
        private readonly SkillsControl _skills = new SkillsControl();
        private readonly HistoryControl _history;
        private readonly TabControl _tabs = new TabControl { Dock = DockStyle.Fill };
        private AppSettings _appSettings;
        private LayoutActionPlan _pendingPlan;
        private TargetSelector _pendingTarget;
        private string _pendingInstruction;
        private CancellationTokenSource _operationCts;

        public MainTaskPaneControl(PowerPoint.Application application, IMcpClient mcp = null)
        {
            _powerPoint = new PowerPointService(application);
            _appSettings = _settingsService.LoadSettings();
            var lmSettings = _settingsService.ToLmStudioSettings(_appSettings);
            _lmStudio = new LmStudioClient(lmSettings);
            _router = new SkillRouter(_lmStudio, _lmStudio);
            _routerService = new LlmRouterService(_lmStudio, () => _appSettings.UseLlmRouter);
            _conversation = new ConversationService(_lmStudio);
            _factCheck = new FactCheckService(_lmStudio, mcp ?? new LmStudioMcpClient(_lmStudio));
            _executor = new ActionExecutor(_powerPoint); _rollback = new RollbackManager(_powerPoint);
            _history = new HistoryControl(_chats);

            var settings = new ModelSettingsControl(_lmStudio, _settingsService, _appSettings);
            settings.Status += text => _chat.AddMessage("assistant", text);
            settings.SettingsChanged += value => { _appSettings = value; UpdateBudget(); };
            _context.ContextChanged += () => { if (_chats.Current != null) { _chats.Current.DeckContext = _context.DeckContextText; _chats.Save(_chats.Current); } UpdateBudget(); };
            _skills.Status += text => _chat.AddMessage("assistant", text);
            _history.Selected += id => ShowHistory(JsonUtils.Serialize(FileStoreUtils.LoadSession(id)));
            _history.RollbackSelected += RollbackSession;
            _history.ChatSelected += chat => ShowHistory(JsonUtils.Serialize(chat));

            var skillsPage = new Panel { Dock = DockStyle.Fill };
            skillsPage.Controls.Add(_skills); skillsPage.Controls.Add(BuildDetailedOperations());
            var commands = new CommandsControl(_router);
            commands.InsertRequested += text => { _tabs.SelectedIndex = 0; _chat.SetInputText(text); };
            commands.Status += text => { _tabs.SelectedIndex = 0; _chat.AddMessage("assistant", text); _skills.RefreshSkills(); };
            _skills.SkillsChanged += () => commands.RefreshCommands();
            _tabs.TabPages.Add(new TabPage("チャット") { Controls = { _chat } });
            _tabs.TabPages.Add(new TabPage("コマンド") { Controls = { commands } });
            _tabs.TabPages.Add(new TabPage("設定") { Controls = { settings } });
            _tabs.TabPages.Add(new TabPage("コンテキスト") { Controls = { _context } });
            _tabs.TabPages.Add(new TabPage("スキル") { Controls = { skillsPage } });
            _tabs.TabPages.Add(new TabPage("履歴") { Controls = { _history } });
            AutoScaleMode = AutoScaleMode.Dpi; AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            Font = Theme.Base; BackColor = Theme.Canvas;
            Controls.Add(_tabs); Dock = DockStyle.Fill;

            _chat.SendRequested += async (text, mode) => await HandleChatAsync(text, mode);
            _chat.ApplyRequested += indexes => ApplyPending(indexes);
            _chat.RollbackRequested += RollbackLast;
            _chat.NewChatRequested += NewChat;
            _chat.SummarizeNewRequested += async () => await SummarizeAndStartNewAsync();
            _chat.HistoryRequested += () => { _history.RefreshSessions(); _tabs.SelectedIndex = 5; };
            _chat.CancelRequested += () => _operationCts?.Cancel();
            _chat.LoadTextModelRequested += async () => await RunModelOperationAsync("Textモデルをロードしています…", () => _lmStudio.TryLoadModelAsync(_appSettings.TextModelName));
            _chat.LoadVisionModelRequested += async () => await RunModelOperationAsync("Visionモデルをロードしています…", () => _lmStudio.TryLoadModelAsync(_appSettings.VisionModelName, true));
            _chat.CheckModelsRequested += async () => await RunModelOperationAsync("モデルの状態を確認しています…", () => _lmStudio.CheckSelectedModelCompatibilityAsync());

            _chats.StartNew(PresentationPath(), _context.DeckContextText);
            _chat.AddMessage("assistant", "こんにちは。スライドの相談も編集依頼も、下の入力欄にそのまま書いてください。\r\n編集する場合は、修正案を提示して確認いただいてから適用します。");
            UpdateBudget();
        }

        private async Task HandleChatAsync(string text, string mode)
        {
            if (_operationCts != null) { _chat.AddMessage("assistant", "別の処理を実行中です。完了を待つか［中止］を押してください。"); return; }
            _operationCts = new CancellationTokenSource(); var cancellationToken = _operationCts.Token; _chat.SetBusy(true);
            _chat.AddMessage("user", text); _chats.AddMessage("user", text); UpdateBudget();
            try
            {
                PlannerResult plan;
                var slash = SlashCommandService.TryParse(text);
                if (slash != null)
                {
                    if (!_router.Has(slash.Skill))
                    {
                        Reply($"不明なコマンドです: /{slash.Skill}\r\n利用可能なコマンド:\r\n" +
                              string.Join("\r\n", _router.SkillNames.OrderBy(x => x).Select(x => "・/" + x)));
                        return;
                    }
                    var policy = _router.GetContextPolicy(slash.Skill);
                    plan = SlashCommandService.ToPlan(slash, policy.IncludeSlideImages, _selectors.Parse(slash.Instruction).Raw);
                }
                else
                {
                    plan = await _routerService.PlanAsync(text, mode, _chats.Current?.Messages, cancellationToken);
                }
                _appSettings.LastSkillMode = mode;
                TrySaveSettings();
                if (plan.Intent == "settings") { Reply("設定タブを開きます。接続・モデル・API keyを確認できます。"); _tabs.SelectedIndex = 2; return; }
                if (plan.Intent == "rollback") { RollbackLast(); return; }

                var target = _selectors.Parse(plan.Target);
                var slides = _powerPoint.ResolveSlides(target).Select(x => _powerPoint.ExtractSlide(x, plan.NeedsVision)).ToList();
                if (plan.Intent == "chat")
                {
                    var answer = await _conversation.ReplyAsync(new ConversationInput
                    {
                        UserText = text,
                        DeckContext = _context.DeckContextText,
                        Summary = _chats.Current?.Summary?.RawSummary,
                        Slides = slides,
                        RecentMessages = HistoryWithoutLastUserMessage(text),
                        MaxHistoryMessages = _appSettings.RecentChatMessages
                    }, cancellationToken);
                    Reply(answer);
                    return;
                }
                if (target.Type == "selected")
                {
                    var ids = new HashSet<int>(_powerPoint.SelectedShapes().Select(x => x.Id));
                    foreach (var slide in slides) slide.Shapes = slide.Shapes.Where(x => ids.Contains(x.Id)).ToList();
                }
                var deck = new DeckContext { RawText = _context.DeckContextText, TimeLimit = plan.Intent == "timing_5min" ? "5分" : null };
                var input = new SkillInput
                {
                    Context = deck, Target = target, Instruction = plan.Instruction, Slides = slides, Style = new DeckStyleAnalyzer().Analyze(slides),
                    RecentMessages = _chats.Current?.Messages?.ToList(), ConversationSummary = _chats.Current?.Summary,
                    RuleBasedIssues = new LayoutAnalysisService().Analyze(slides)
                };
                if (plan.Intent == "fact_check")
                {
                    var result = await _factCheck.CheckAsync(target.Raw, slides, deck, cancellationToken);
                    _pendingPlan = null; var message = FormatFactCheck(result); Reply(message, "fact_check", JsonUtils.Serialize(result)); _chat.ShowResult(message); return;
                }
                _pendingPlan = await _router.RunPlannedAsync(plan, input, cancellationToken);
                _pendingTarget = target; _pendingInstruction = text;
                var card = FormatPlan(plan, _pendingPlan);
                Reply(card, "action_plan", JsonUtils.Serialize(_pendingPlan)); _chat.ShowResult(card, _pendingPlan);
            }
            catch (OperationCanceledException) { Reply("処理を中止しました。PowerPointへの変更は行っていません。", "cancelled"); }
            catch (Exception ex) { ShowError(ex); }
            finally { _operationCts.Dispose(); _operationCts = null; _chat.SetBusy(false); UpdateBudget(); }
        }

        private void ApplyPending(IReadOnlyList<int> indexes)
        {
            try
            {
                if (_pendingPlan == null || _pendingTarget == null) throw new InvalidOperationException("適用する修正案がありません。");
                var selected = indexes?.Distinct().Where(x => x >= 0 && x < _pendingPlan.Actions.Count).ToList() ?? new List<int>();
                if (selected.Count == 0) throw new InvalidOperationException("適用するActionを選択してください。");
                var plan = new LayoutActionPlan { Skill = _pendingPlan.Skill, Target = _pendingPlan.Target, Summary = _pendingPlan.Summary, Issues = _pendingPlan.Issues };
                plan.Actions = selected.Select(x => _pendingPlan.Actions[x]).ToList();
                var session = _executor.Execute(plan, _pendingTarget, _pendingInstruction);
                Reply($"AI編集を適用しました。\r\nSession: {session.SessionId}\r\n変更: {plan.Actions.Count}件", "applied", JsonUtils.Serialize(session));
                _history.RefreshSessions();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private void RollbackLast()
        {
            try { var session = _rollback.RollbackLast(); Reply("直前のAI編集を戻しました。\r\nSession: " + session.SessionId, "rollback"); _history.RefreshSessions(); }
            catch (Exception ex) { ShowError(ex); }
        }
        private void RollbackSession(string sessionId)
        {
            try { _rollback.Rollback(sessionId); Reply("選択したAI編集を戻しました。\r\nSession: " + sessionId, "rollback"); _history.RefreshSessions(); }
            catch (Exception ex) { ShowError(ex); }
        }
        private void NewChat()
        {
            _chats.StartNew(PresentationPath(), _context.DeckContextText); _chat.ClearConversation(); _chat.AddMessage("assistant", "新しいチャットを開始しました。Deck ContextとAI編集履歴は維持しています。");
            _pendingPlan = null; _pendingTarget = null; UpdateBudget();
        }
        private async Task SummarizeAndStartNewAsync()
        {
            try
            {
                var summary = await new ChatSummarizer(_lmStudio).SummarizeAsync(_chats.Current, _appSettings);
                _chats.StartNew(PresentationPath(), _context.DeckContextText, summary); _chat.ClearConversation();
                _chat.AddMessage("assistant", "前のチャットを要約して新しいチャットを開始しました。\r\n\r\n" + summary.RawSummary); UpdateBudget();
            }
            catch (Exception ex) { ShowError(ex); }
        }

        private Control BuildDetailedOperations()
        {
            var panel = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 74, WrapContents = true };
            Add(panel, "現在スライド解析", () => ShowHistory(JsonUtils.Serialize(_powerPoint.ExtractSlide(_powerPoint.CurrentSlide, true))));
            Add(panel, "PNG書き出し", () => ShowHistory(_powerPoint.ExportPng(_powerPoint.CurrentSlide)));
            Add(panel, "Shape JSON", () => ShowHistory(JsonUtils.Serialize(_powerPoint.ExtractSlide(_powerPoint.CurrentSlide))));
            Add(panel, "診断ログ", () => ShowHistory(DiagnosticLogService.ReadRecent()));
            Add(panel, "選択を右へ", () => { foreach (var shape in _powerPoint.SelectedShapes()) _powerPoint.Move(shape, 10, 0); });
            Add(panel, "選択を左へ", () => { foreach (var shape in _powerPoint.SelectedShapes()) _powerPoint.Move(shape, -10, 0); });
            Add(panel, "中央揃え", () => _powerPoint.Align(_powerPoint.SelectedShapes(), "center_horizontal"));
            Add(panel, "等間隔", () => _powerPoint.Distribute(_powerPoint.SelectedShapes(), "horizontal"));
            return panel;
        }
        private async Task RunModelOperationAsync(string startMessage, Func<Task<string>> operation)
        {
            if (_operationCts != null) { _chat.AddMessage("assistant", "別の処理を実行中です。完了を待ってください。"); return; }
            _operationCts = new CancellationTokenSource(); _chat.SetBusy(true);
            _chat.AddMessage("assistant", startMessage);
            try { Reply(await operation()); }
            catch (Exception ex) { ShowError(ex); }
            finally { _operationCts.Dispose(); _operationCts = null; _chat.SetBusy(false); }
        }

        private List<ChatMessage> HistoryWithoutLastUserMessage(string text)
        {
            var messages = _chats.Current?.Messages?.ToList() ?? new List<ChatMessage>();
            if (messages.Count > 0 && messages[messages.Count - 1].Role == "user" && messages[messages.Count - 1].Content == text)
                messages.RemoveAt(messages.Count - 1);
            return messages;
        }

        private void Reply(string text, string kind = "message", string metadata = null) { _chat.AddMessage("assistant", text); _chats.AddMessage("assistant", text, kind, metadata); }
        private void ShowError(Exception ex)
        {
            DiagnosticLogService.Error("ui_operation_failed", ex);
            var error = _errors.Convert(ex); _chat.AddMessage("error", error.ToString()); _chat.ShowResult(error + "\r\n\r\n技術詳細はチャット履歴に保存しました。");
            _chats.AddMessage("assistant", error.ToString(), "error", error.TechnicalDetails);
        }
        private void ShowHistory(string value) { _chat.ShowResult(value); }
        private void UpdateBudget()
        {
            var parts = new List<string> { _context.DeckContextText, _pendingPlan == null ? string.Empty : JsonUtils.Serialize(_pendingPlan) };
            if (_chats.Current?.Messages != null) parts.AddRange(_chats.Current.Messages.Select(x => x.Content));
            _chat.UpdateBudget(_budget.Calculate(parts, _appSettings.ContextLength));
        }
        private void TrySaveSettings() { try { _settingsService.SaveSettings(_appSettings); } catch { } }
        private string PresentationPath() { try { return _powerPoint.CurrentPresentation.FullName; } catch { return string.Empty; } }
        private static string FormatPlan(PlannerResult planner, LayoutActionPlan plan)
        {
            if ((plan.Actions?.Count ?? 0) == 0 && (plan.Issues?.Count ?? 0) == 0)
            {
                var readContext = plan.ContextReport == null ? string.Empty : $"\r\n\r\n送信コンテキスト: 約{plan.ContextReport.EstimatedInputTokens:N0}/{plan.ContextReport.InputBudgetTokens:N0} tokens" +
                    (plan.ContextReport.ActualInputTokens.HasValue ? $"（API実測 {plan.ContextReport.ActualInputTokens:N0}）" : string.Empty) + $"、Slide {plan.ContextReport.IncludedSlides}枚、会話{plan.ContextReport.IncludedMessages}件";
                return $"{planner.Target} を参照しました。\r\n\r\n{plan.Summary}{readContext}";
            }
            var issues = plan.Issues?.Take(8).Select(x => $"・{x.Slide}枚目: {x.Message}").ToList() ?? new List<string>();
            var actions = plan.Actions?.Take(10).Select(x => $"・{x.Slide}枚目: {x.Type} — {x.Reason}").ToList() ?? new List<string>();
            var context = plan.ContextReport == null ? string.Empty : $"\r\n\r\n送信コンテキスト: 約{plan.ContextReport.EstimatedInputTokens:N0}/{plan.ContextReport.InputBudgetTokens:N0} tokens" +
                (plan.ContextReport.ActualInputTokens.HasValue ? $"（API実測 {plan.ContextReport.ActualInputTokens:N0}）" : string.Empty) + $"、Slide {plan.ContextReport.IncludedSlides}枚、会話{plan.ContextReport.IncludedMessages}件" +
                (plan.ContextReport.OmittedOrTruncated.Count == 0 ? string.Empty : "\r\n省略・圧縮: " + string.Join(", ", plan.ContextReport.OmittedOrTruncated));
            return $"{planner.Target} を確認しました。\r\n\r\n判断:\r\n{planner.Explanation}\r\n\r\n主な問題 ({plan.Issues?.Count ?? 0}件):\r\n{string.Join("\r\n", issues)}\r\n\r\n作成した修正案 ({plan.Actions?.Count ?? 0}件):\r\n{string.Join("\r\n", actions)}{context}";
        }
        private static string FormatFactCheck(FactCheckResult result)
        {
            var groups = result.Claims.GroupBy(x => x.Status).OrderBy(x => x.Key).Select(x => $"・{x.Key}: {x.Count()}件");
            var citations = result.Claims.Where(x => x.Status == "needs_source" || x.Status == "overstated").Take(8).Select(x => $"・スライド{x.Slide}: {x.Claim}");
            var context = result.ContextReport == null ? string.Empty : $"\r\n\r\n送信コンテキスト: 約{result.ContextReport.EstimatedInputTokens:N0}/{result.ContextReport.InputBudgetTokens:N0} tokens" +
                (result.ContextReport.ActualInputTokens.HasValue ? $"（API実測 {result.ContextReport.ActualInputTokens:N0}）" : string.Empty);
            return $"{result.Target} から{result.Claims.Count}個の主張を抽出しました。\r\n\r\n結果:\r\n{string.Join("\r\n", groups)}\r\n\r\n引用・表現修正が必要な主張:\r\n{string.Join("\r\n", citations)}{context}";
        }
        private static void Add(Control parent, string text, Action action) { var button = new Button { Text = text, AutoSize = true }; button.Click += (s, e) => { try { action(); } catch { } }; parent.Controls.Add(button); }
    }
}

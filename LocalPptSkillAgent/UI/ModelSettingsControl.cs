using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Services;

namespace LocalPptSkillAgent.UI
{
    public class ModelSettingsControl : UserControl
    {
        private readonly LmStudioClient _client;
        private readonly SettingsService _service;
        private AppSettings _settings;
        private readonly TextBox _baseUrl = Box();
        private readonly TextBox _endpoint = Box();
        private readonly TextBox _nativeEndpoint = Box();
        private readonly TextBox _apiKey = new TextBox { Dock = DockStyle.Top, UseSystemPasswordChar = true };
        private readonly ComboBox _textModel = Combo();
        private readonly ComboBox _visionModel = Combo();
        private readonly NumericUpDown _temperature = Number(0, 2, 0.1m, 1);
        private readonly NumericUpDown _topP = Number(0.1m, 1, 0.05m, 2);
        private readonly NumericUpDown _seed = Number(-1, int.MaxValue, 1, 0);
        private readonly NumericUpDown _maxTokens = Number(128, 131072, 256, 0);
        private readonly NumericUpDown _context = Number(1024, 262144, 1024, 0);
        private readonly NumericUpDown _visionContext = Number(1024, 262144, 1024, 0);
        private readonly NumericUpDown _evalBatch = Number(32, 8192, 32, 0);
        private readonly CheckBox _flashAttention = new CheckBox { Dock = DockStyle.Top, Text = "Flash Attention", AutoSize = true };
        private readonly CheckBox _offloadKv = new CheckBox { Dock = DockStyle.Top, Text = "KV cacheをGPUへoffload", AutoSize = true };
        private readonly NumericUpDown _safetyMargin = Number(256, 32768, 256, 0);
        private readonly NumericUpDown _recentMessages = Number(0, 100, 1, 0);
        private readonly NumericUpDown _timeout = Number(15, 1800, 15, 0);
        private readonly NumericUpDown _structuredRetries = Number(0, 3, 1, 0);
        private readonly ComboBox _textReasoning = Combo();
        private readonly NumericUpDown _textReasoningBudget = Number(128, 16384, 128, 0);
        private readonly CheckBox _structured = new CheckBox { Dock = DockStyle.Top, Text = "JSON Schemaを使用する", AutoSize = true };
        private readonly CheckBox _llmRouter = new CheckBox { Dock = DockStyle.Top, Text = "曖昧な発話の意図をLLMで判定(低速)", AutoSize = true };
        private readonly ComboBox _apiMode = Combo();
        private readonly ComboBox _visionMode = Combo();
        private readonly TextBox _mcpSearch = Box();
        public event Action<string> Status;
        public event Action<AppSettings> SettingsChanged;

        public ModelSettingsControl(LmStudioClient client, SettingsService service, AppSettings settings)
        {
            _client = client; _service = service; _settings = settings;
            _apiMode.Items.AddRange(new object[] { "openai_compatible", "lmstudio_native" });
            _visionMode.Items.AddRange(new object[] { "image_url_base64", "native_image" });
            _textReasoning.Items.AddRange(new object[] { "none", "minimal", "low", "medium", "high", "xhigh" });
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            Font = Theme.Base; BackColor = Theme.Canvas;

            // 操作ボタンはスクロールの外(上部固定)に置く
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, BackColor = Theme.Card, Padding = new Padding(4, 4, 4, 2) };
            AddPrimary(buttons, "保存", Save);
            Add(buttons, "接続確認", async () => await Check());
            Add(buttons, "モデル一覧更新", async () => await RefreshModels());
            Add(buttons, "保存済みを読み込む", LoadSavedSettings);
            Add(buttons, "初期値に戻す", Reset);
            var testButtons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, WrapContents = true, BackColor = Theme.Card, Padding = new Padding(4, 0, 4, 4) };
            Add(testButtons, "Textモデルをロード", async () => await Run(() => _client.TryLoadModelAsync(_textModel.Text)));
            Add(testButtons, "Textをアンロード", async () => await Run(() => _client.TryUnloadModelAsync(_textModel.Text)));
            Add(testButtons, "Visionをロード", async () => await Run(() => _client.TryLoadModelAsync(_visionModel.Text, true)));
            Add(testButtons, "Visionをアンロード", async () => await Run(() => _client.TryUnloadModelAsync(_visionModel.Text)));
            Add(testButtons, "応答テスト", async () => await Run(() => _client.ChatAsync("短く応答してください。", "接続テスト。OKと返してください。")));
            Add(testButtons, "JSON出力テスト", async () => await Run(() => _client.TestStructuredOutputAsync()));
            Add(testButtons, "モデル適合確認", async () => await Run(() => _client.CheckSelectedModelCompatibilityAsync()));
            Add(testButtons, "SLM適合テスト", async () => await Run(async () => (await new SlmQualificationService(_client).RunAsync()).ToString()));

            // Dock=Topは後に追加したものほど上に表示されるため、画面表示の逆順で追加する
            var fields = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Theme.Canvas, Padding = new Padding(8, 0, 8, 8) };
            fields.Controls.Add(Labeled("検索系MCP ID(カンマ区切り)", _mcpSearch));
            fields.Controls.Add(Section("ファクトチェック連携"));
            fields.Controls.Add(Labeled("Vision画像の入力方式", _visionMode));
            fields.Controls.Add(Labeled("APIモード", _apiMode));
            fields.Controls.Add(Labeled("KV cacheをGPUに置く", _offloadKv));
            fields.Controls.Add(Labeled("Flash Attention", _flashAttention));
            fields.Controls.Add(Labeled("Eval batch size(ロード時)", _evalBatch));
            fields.Controls.Add(Section("詳細(通常は変更不要)"));
            fields.Controls.Add(Labeled("リクエストタイムアウト(秒)", _timeout));
            fields.Controls.Add(Labeled("会話に含める直近メッセージ数", _recentMessages));
            fields.Controls.Add(Labeled("入力安全マージン(tokens)", _safetyMargin));
            fields.Controls.Add(Labeled("Vision用Context length(tokens)", _visionContext));
            fields.Controls.Add(Labeled("Context length(tokens)", _context));
            fields.Controls.Add(Section("コンテキスト"));
            fields.Controls.Add(Labeled("JSON修復の再試行回数", _structuredRetries));
            fields.Controls.Add(Labeled("意図判定をLLMに任せる(既定はキーワード)", _llmRouter));
            fields.Controls.Add(Labeled("JSON Schema出力を使う(推奨)", _structured));
            fields.Controls.Add(Labeled("内部思考のトークン上限", _textReasoningBudget));
            fields.Controls.Add(Labeled("内部思考の強さ(reasoning effort)", _textReasoning));
            fields.Controls.Add(Labeled("最大出力トークン(Max tokens)", _maxTokens));
            fields.Controls.Add(Labeled("Seed(-1=ランダム)", _seed));
            fields.Controls.Add(Labeled("Top P", _topP));
            fields.Controls.Add(Labeled("Temperature(低いほど堅実)", _temperature));
            fields.Controls.Add(Section("生成パラメータ"));
            fields.Controls.Add(Labeled("Visionモデル(スライド画像の解析用)", _visionModel));
            fields.Controls.Add(Labeled("Textモデル(会話と編集案の生成用)", _textModel));
            fields.Controls.Add(Section("モデル"));
            fields.Controls.Add(Labeled("APIキー(DPAPIで暗号化保存)", _apiKey));
            fields.Controls.Add(Labeled("Native APIエンドポイント(MCP用)", _nativeEndpoint));
            fields.Controls.Add(Labeled("Chat completionsエンドポイント", _endpoint));
            fields.Controls.Add(Labeled("Base URL(LM Studioのアドレス)", _baseUrl));
            fields.Controls.Add(Section("LM Studio接続"));
            Controls.Add(fields);
            Controls.Add(testButtons);
            Controls.Add(buttons);
            Dock = DockStyle.Fill;
            ShowSettings(_settings);
        }

        public AppSettings CaptureSettings()
        {
            _settings.BaseUrl = _baseUrl.Text.Trim().TrimEnd('/');
            _settings.ChatCompletionsEndpoint = _endpoint.Text.Trim();
            _settings.NativeChatEndpoint = _nativeEndpoint.Text.Trim();
            _settings.ApiKey = _apiKey.Text;
            _settings.TextModelName = _textModel.Text.Trim(); _settings.VisionModelName = _visionModel.Text.Trim();
            _settings.Temperature = (double)_temperature.Value; _settings.TopP = (double)_topP.Value; _settings.Seed = (int)_seed.Value;
            _settings.MaxTokens = (int)_maxTokens.Value; _settings.ContextLength = (int)_context.Value;
            _settings.VisionContextLength = (int)_visionContext.Value; _settings.EvalBatchSize = (int)_evalBatch.Value;
            _settings.FlashAttention = _flashAttention.Checked; _settings.OffloadKvCacheToGpu = _offloadKv.Checked;
            _settings.InputSafetyMarginTokens = (int)_safetyMargin.Value; _settings.RecentChatMessages = (int)_recentMessages.Value;
            _settings.RequestTimeoutSeconds = (int)_timeout.Value; _settings.UseStructuredOutput = _structured.Checked; _settings.StructuredOutputRetryCount = (int)_structuredRetries.Value;
            _settings.UseLlmRouter = _llmRouter.Checked;
            _settings.TextReasoningEffort = _textReasoning.Text;
            _settings.TextReasoningBudgetTokens = (int)_textReasoningBudget.Value;
            _settings.ApiMode = _apiMode.Text; _settings.VisionInputMode = _visionMode.Text;
            _settings.McpSearchIntegrationIds = Split(_mcpSearch.Text);
            return _settings;
        }

        private void Save()
        {
            try { CaptureSettings(); _service.SaveSettings(_settings); _service.ApplyTo(_settings, _client.Settings); Status?.Invoke("設定を保存しました。"); SettingsChanged?.Invoke(_settings); }
            catch (Exception ex) { Status?.Invoke("設定を保存できませんでした。\r\n" + ex.Message); }
        }
        private void LoadSavedSettings()
        {
            _settings = _service.LoadSettings(); ShowSettings(_settings); _service.ApplyTo(_settings, _client.Settings);
            Status?.Invoke("設定を読み込みました。"); SettingsChanged?.Invoke(_settings);
        }
        private void Reset()
        {
            _settings = _service.ResetSettings(); ShowSettings(_settings); _service.ApplyTo(_settings, _client.Settings);
            Status?.Invoke("設定を初期値へ戻しました。"); SettingsChanged?.Invoke(_settings);
        }
        private async Task Check()
        {
            try { CaptureSettings(); _service.ApplyTo(_settings, _client.Settings); Status?.Invoke(await _client.CheckConnectionAsync()); }
            catch { Status?.Invoke("LM Studioに接続できませんでした。\r\n確認してください:\r\n1. LM Studioが起動しているか\r\n2. Local ServerがStartされているか\r\n3. EndpointとAPI keyが正しいか\r\n4. モデルがロードされているか"); }
        }
        private async Task RefreshModels()
        {
            try
            {
                CaptureSettings(); _service.ApplyTo(_settings, _client.Settings);
                var models = await _client.GetModelsAsync(); var text = _textModel.Text; var vision = _visionModel.Text;
                _textModel.Items.Clear(); _visionModel.Items.Clear(); _textModel.Items.AddRange(models.Cast<object>().ToArray()); _visionModel.Items.AddRange(models.Cast<object>().ToArray());
                _textModel.Text = text; _visionModel.Text = vision; Status?.Invoke($"モデル一覧: {models.Count}件");
            }
            catch (Exception ex) { Status?.Invoke("モデル一覧を取得できませんでした。\r\n" + ex.Message); }
        }
        private async Task Run(Func<Task<string>> action)
        {
            try { CaptureSettings(); _service.ApplyTo(_settings, _client.Settings); Status?.Invoke(await action()); }
            catch (Exception ex) { Status?.Invoke("LM Studioの処理に失敗しました。\r\n" + ex.Message); }
        }
        private void ShowSettings(AppSettings value)
        {
            _baseUrl.Text = value.BaseUrl; _endpoint.Text = value.ChatCompletionsEndpoint; _nativeEndpoint.Text = value.NativeChatEndpoint;
            _apiKey.Text = value.ApiKey ?? string.Empty; _textModel.Text = value.TextModelName ?? string.Empty; _visionModel.Text = value.VisionModelName ?? string.Empty;
            _temperature.Value = Clamp((decimal)value.Temperature, _temperature); _maxTokens.Value = Clamp(value.MaxTokens, _maxTokens); _context.Value = Clamp(value.ContextLength, _context);
            _visionContext.Value = Clamp(value.VisionContextLength < 1024 ? 82778 : value.VisionContextLength, _visionContext);
            _evalBatch.Value = Clamp(value.EvalBatchSize < 32 ? 2048 : value.EvalBatchSize, _evalBatch);
            _flashAttention.Checked = value.FlashAttention; _offloadKv.Checked = value.OffloadKvCacheToGpu;
            _topP.Value = Clamp((decimal)value.TopP, _topP); _seed.Value = Clamp(value.Seed, _seed); _safetyMargin.Value = Clamp(value.InputSafetyMarginTokens, _safetyMargin);
            _recentMessages.Value = Clamp(value.RecentChatMessages, _recentMessages); _timeout.Value = Clamp(value.RequestTimeoutSeconds, _timeout);
            _structured.Checked = value.UseStructuredOutput; _structuredRetries.Value = Clamp(value.StructuredOutputRetryCount, _structuredRetries);
            _llmRouter.Checked = value.UseLlmRouter;
            _textReasoning.Text = string.IsNullOrWhiteSpace(value.TextReasoningEffort) ? "low" : value.TextReasoningEffort;
            _textReasoningBudget.Value = Clamp(value.TextReasoningBudgetTokens < 128 ? 1000 : value.TextReasoningBudgetTokens, _textReasoningBudget);
            _apiMode.Text = value.ApiMode; _visionMode.Text = value.VisionInputMode; _mcpSearch.Text = string.Join(",", value.McpSearchIntegrationIds ?? new List<string>());
        }
        private static decimal Clamp(decimal value, NumericUpDown control) => Math.Max(control.Minimum, Math.Min(control.Maximum, value));
        private static List<string> Split(string value) => (value ?? string.Empty).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(x => x.Trim()).Where(x => x.Length > 0).ToList();
        private static TextBox Box() => new TextBox { Dock = DockStyle.Top };
        private static ComboBox Combo() => new ComboBox { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDown };
        private static NumericUpDown Number(decimal min, decimal max, decimal increment, int decimals) => new NumericUpDown { Dock = DockStyle.Top, Minimum = min, Maximum = max, Increment = increment, DecimalPlaces = decimals };
        private static Control Section(string text)
        {
            var label = new Label { Text = text, Dock = DockStyle.Top, Height = 30, Font = Theme.Title, ForeColor = Theme.Accent, TextAlign = System.Drawing.ContentAlignment.BottomLeft };
            return label;
        }
        private static Control Labeled(string text, Control control)
        {
            var panel = new Panel { Dock = DockStyle.Top, Height = 46 };
            panel.Controls.Add(control);
            panel.Controls.Add(new Label { Text = text, Dock = DockStyle.Top, Height = 18, Font = Theme.Small, ForeColor = Theme.TextMuted });
            return panel;
        }
        private static void Add(Control parent, string text, Action action) { var b = new Button { Text = text, AutoSize = true }; Theme.StyleSecondary(b); b.Click += (s, e) => action(); parent.Controls.Add(b); }
        private static void Add(Control parent, string text, Func<Task> action) { var b = new Button { Text = text, AutoSize = true }; Theme.StyleSecondary(b); b.Click += async (s, e) => await action(); parent.Controls.Add(b); }
        private static void AddPrimary(Control parent, string text, Action action) { var b = new Button { Text = text, AutoSize = true }; Theme.StylePrimary(b); b.Click += (s, e) => action(); parent.Controls.Add(b); }
    }
}

using System;
using System.IO;
using System.Net.Http;
using LocalPptSkillAgent.Models;
using LocalPptSkillAgent.Services;
using LocalPptSkillAgent.Skills;
using LocalPptSkillAgent.Utils;
using Xunit;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace LocalPptSkillAgent.Tests
{
    public class ChatFirstServicesTests
    {
        [Fact]
        public void SettingsRoundTripEncryptsApiKeyWithDpapi()
        {
            var directory = CreateTempDirectory();
            try
            {
                var path = Path.Combine(directory, "settings.json");
                var service = new SettingsService(path);
                var settings = new AppSettings { ApiKey = "test-secret-value", TextModelName = "text-model", TopP = 0.75, Seed = 42, UseStructuredOutput = true };

                service.SaveSettings(settings);

                var json = File.ReadAllText(path);
                Assert.DoesNotContain("test-secret-value", json);
                Assert.False(string.IsNullOrWhiteSpace(settings.ProtectedApiKey));
                Assert.Equal("test-secret-value", service.LoadSettings().ApiKey);
                Assert.Equal(0.75, service.LoadSettings().TopP);
                Assert.Equal(42, service.LoadSettings().Seed);
                Assert.Equal("test*********alue", service.MaskApiKeyForDisplay("test-secret-value"));
            }
            finally { Directory.Delete(directory, true); }
        }

        [Theory]
        [InlineData("@1-5 見た目を整えて", "@1-5", "visual_polish", true, false)]
        [InlineData("@3 PowerPoint図形で模式図にして", "@3", "diagram_create", true, false)]
        [InlineData("@2-6 ファクトチェックして", "@2-6", "fact_check", false, true)]
        [InlineData("さっきの編集を戻して", "@current", "rollback", false, false)]
        [InlineData("@all 英語に翻訳して", "@all", "translate", false, false)]
        [InlineData("このデッキを要約して", "@all", "deck_summary", false, false)]
        [InlineData("メンデル遺伝の法則を説明する3枚のスライド作って", "@current", "slide_generate", false, false)]
        public void PlannerRoutesNaturalLanguage(string input, string target, string intent, bool vision, bool mcp)
        {
            var plan = new PlannerSkill().Plan(input);
            Assert.Equal(target, plan.Target);
            Assert.Equal(intent, plan.Intent);
            Assert.Equal(vision, plan.NeedsVision);
            Assert.Equal(mcp, plan.NeedsMcp);
            Assert.Contains("\"needs_vision\"", new PlannerSkill().PlanJson(input));
        }

        [Theory]
        [InlineData("このデッキの構成についてどう思う?")]
        [InlineData("発表で緊張しないコツを教えて")]
        [InlineData("ありがとう、助かった")]
        public void PlannerFallsBackToChatForFreeConversation(string input)
        {
            var plan = new PlannerSkill().Plan(input);
            Assert.Equal("chat", plan.Intent);
            Assert.Empty(plan.Skills);
            Assert.False(plan.RequiresPreview);
            Assert.False(plan.RequiresUserConfirmation);
        }

        [Fact]
        public void BuildResultRejectsUnknownIntentAsChat()
        {
            Assert.Equal("chat", PlannerSkill.BuildResult("hallucinated_intent", "@current", "テスト").Intent);
            Assert.Equal("visual_polish", PlannerSkill.BuildResult("VISUAL_POLISH", "@current", "テスト").Intent);
        }

        [Theory]
        [InlineData("<think>内部検討</think>回答です", "回答です", 17)]
        [InlineData("回答のみ", "回答のみ", 0)]
        [InlineData("<think>閉じタグなしの内部検討", "", 15)]
        [InlineData("前半<think>a</think>中盤<think>b</think>後半", "前半中盤後半", 0)]
        public void StripThinkBlocksRemovesQwenReasoning(string input, string expected, int _)
        {
            Assert.Equal(expected, LmStudioClient.StripThinkBlocks(input, out var chars));
            if (input != expected) Assert.True(chars > 0);
        }

        [Fact]
        public async System.Threading.Tasks.Task LlmRouterUsesKeywordPlannerByDefault()
        {
            // 既定(UseLlmRouter=false)ではLLMを呼ばず、決定的なキーワード判定を返す
            using (var client = new LmStudioClient(new LmStudioSettings { TextModel = "any" }))
            {
                var router = new LlmRouterService(client, () => false);
                var edit = await router.PlanAsync("@1-5 見た目を整えて", "auto", null, System.Threading.CancellationToken.None);
                Assert.False(router.LastUsedLlm);
                Assert.Equal("visual_polish", edit.Intent);
                Assert.Equal("@1-5", edit.Target);

                var rollback = await router.PlanAsync("さっきの編集を戻して", "auto", null, System.Threading.CancellationToken.None);
                Assert.Equal("rollback", rollback.Intent);
                Assert.False(router.LastUsedLlm);

                var chat = await router.PlanAsync("このデッキどう思う?", "auto", null, System.Threading.CancellationToken.None);
                Assert.Equal("chat", chat.Intent);
                Assert.False(router.LastUsedLlm);
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task LlmRouterDoesNotEscalateRollbackOrClearEditsEvenWhenEnabled()
        {
            // 有効化してもrollbackや明確な編集はキーワード優先でLLMに送らない(TextModel未設定でも例外なく通る)
            using (var client = new LmStudioClient(new LmStudioSettings { TextModel = null }))
            {
                var router = new LlmRouterService(client, () => true);
                var rollback = await router.PlanAsync("さっきの編集を戻して", "auto", null, System.Threading.CancellationToken.None);
                Assert.Equal("rollback", rollback.Intent);
                Assert.False(router.LastUsedLlm);

                var edit = await router.PlanAsync("@3 校閲して", "auto", null, System.Threading.CancellationToken.None);
                Assert.Equal("proofread", edit.Intent);
                Assert.False(router.LastUsedLlm);
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task LlmRouterSkipsLlmWhenModeIsForced()
        {
            using (var client = new LmStudioClient(new LmStudioSettings { TextModel = null }))
            {
                var plan = await new LlmRouterService(client, () => true).PlanAsync("これを直して", "翻訳", null, System.Threading.CancellationToken.None);
                Assert.Equal("translate", plan.Intent);
            }
        }

        [Fact]
        public async System.Threading.Tasks.Task LlmRouterKeepsChatWhenEnabledButLlmUnreachable()
        {
            // 有効化+chat候補でも、LLM到達不可(TextModel未設定)ならchatのまま安全側に倒す
            using (var client = new LmStudioClient(new LmStudioSettings { TextModel = null }))
            {
                var router = new LlmRouterService(client, () => true);
                var chat = await router.PlanAsync("このスライドの流れ良くない?", "auto", null, System.Threading.CancellationToken.None);
                Assert.Equal("chat", chat.Intent);
            }
        }

        [Theory]
        [InlineData("/proofread @all 誤字を直して", "proofread", "@all 誤字を直して")]
        [InlineData("/deck_summary", "deck_summary", "")]
        [InlineData("/My_Skill テスト", "my_skill", "テスト")]
        public void SlashCommandParsesSkillAndInstruction(string input, string skill, string instruction)
        {
            var command = SlashCommandService.TryParse(input);
            Assert.NotNull(command);
            Assert.Equal(skill, command.Skill);
            Assert.Equal(instruction, command.Instruction);
        }

        [Theory]
        [InlineData("proofreadして")]
        [InlineData("@all 直して")]
        [InlineData("/日本語名 テスト")]
        [InlineData("")]
        public void SlashCommandRejectsNonCommands(string input)
        {
            Assert.Null(SlashCommandService.TryParse(input));
        }

        [Fact]
        public void SlashCommandBuildsDirectSkillPlan()
        {
            var plan = SlashCommandService.ToPlan(new SlashCommand { Skill = "my_skill", Instruction = "@2 整理して" }, true, "@2");
            Assert.Equal("my_skill", plan.Intent);
            Assert.Equal("my_skill", Assert.Single(plan.Skills));
            Assert.Equal("@2", plan.Target);
            Assert.True(plan.NeedsVision);
            Assert.False(plan.NeedsMcp);
        }

        [Fact]
        public void SkillRouterRegistersCustomMarkdownSkills()
        {
            var directory = CreateTempDirectory();
            try
            {
                var content = "## 目的\nノートを要約する\n\n## 出力JSON schema\nLayoutActionPlan標準。\n\n## 禁止事項\n削除しない\n\n## 例\n{}\n\n## Context Policy\n- include_geometry: false\n- include_notes: true\n- include_slide_images: false\n- recent_chat_messages: 4";
                File.WriteAllText(Path.Combine(directory, "summary_notes.md"), content);
                File.WriteAllText(Path.Combine(directory, "Bad-Name.md"), content);
                using (var client = new LmStudioClient())
                {
                    var router = new SkillRouter(client, client, directory);
                    Assert.True(router.Has("summary_notes"));
                    Assert.False(router.Has("bad-name"));
                    var policy = router.GetContextPolicy("summary_notes");
                    Assert.True(policy.IncludeNotes);
                    Assert.False(policy.IncludeSlideImages);
                    Assert.Equal(4, policy.RecentChatMessages);
                }
            }
            finally { Directory.Delete(directory, true); }
        }

        [Theory]
        [InlineData("3枚のスライド作って", 3)]
        [InlineData("10 枚で説明", 10)]
        [InlineData("枚数指定なし", null)]
        public void ResearchSlideWriterReadsRequestedSlideCount(string input, int? expected)
        {
            Assert.Equal(expected, ResearchSlideWriterSkill.RequestedSlideCount(input));
        }

        [Theory]
        [InlineData(400, "安全")]
        [InlineData(600, "注意")]
        [InlineData(750, "危険")]
        [InlineData(850, "上限付近")]
        public void ContextBudgetUsesFourStatusLevels(int japaneseCharacters, string expected)
        {
            var budget = new ContextBudgetService().Calculate(new[] { new string('あ', japaneseCharacters) }, 1024);
            Assert.Equal(expected, budget.Status);
            Assert.Equal(1024, budget.MaxContextTokens);
            Assert.Equal(1024 - budget.EstimatedUsedTokens > 0 ? 1024 - budget.EstimatedUsedTokens : 0, budget.EstimatedRemainingTokens);
        }

        [Fact]
        public void ChatSessionIsSavedAndListed()
        {
            var directory = CreateTempDirectory();
            try
            {
                var manager = new ChatSessionManager(directory);
                var session = manager.StartNew("deck.pptx", "研究発表");
                manager.AddMessage("user", "@current 見た目を確認して");

                Assert.True(File.Exists(Path.Combine(directory, "chat_" + session.ChatSessionId + ".json")));
                var loaded = Assert.Single(manager.List());
                Assert.Equal("deck.pptx", loaded.PresentationPath);
                Assert.Single(loaded.Messages);
            }
            finally { Directory.Delete(directory, true); }
        }

        [Fact]
        public void ConnectionErrorsBecomeUserFriendly()
        {
            var error = new ErrorMessageService().Convert(new HttpRequestException("connection refused"));
            Assert.Equal("LM Studioに接続できません", error.Title);
            Assert.NotEmpty(error.SuggestedActions);
            Assert.Contains("connection refused", error.TechnicalDetails);
        }

        [Fact]
        public void VisionErrorsHaveSpecificGuidance()
        {
            var error = new ErrorMessageService().Convert(new InvalidOperationException("vision image input failed"));
            Assert.Equal("スライド画像の解析に失敗しました", error.Title);
        }

        [Fact]
        public void EmptyReasoningOnlyResponseHasSpecificGuidance()
        {
            var error = new ErrorMessageService().Convert(new InvalidDataException("LM Studioが本文を返しませんでした。reasoning_chars=100"));
            Assert.Equal("Textモデルが回答本文を返しませんでした", error.Title);
            Assert.Contains(error.SuggestedActions, x => x.Contains("reasoning effort"));
        }

        [Fact]
        public void ModelTimeoutHasSpecificGuidance()
        {
            var error = new ErrorMessageService().Convert(new TimeoutException("LM Studioの応答が15秒以内に完了しませんでした。"));
            Assert.Equal("Textモデルの応答待ちがタイムアウトしました", error.Title);
            Assert.Contains(error.SuggestedActions, x => x.Contains("300秒"));
        }

        [Fact]
        public void ChatSummaryAcceptsPlannerSnakeCaseSchema()
        {
            var summary = JsonUtils.Deserialize<ChatSummary>("{\"deck_purpose\":\"研究発表\",\"pending_tasks\":[\"図解\"],\"raw_summary\":\"引継ぎ\"}");
            Assert.Equal("研究発表", summary.DeckPurpose);
            Assert.Equal("図解", Assert.Single(summary.PendingTasks));
            Assert.Equal("引継ぎ", summary.RawSummary);
        }

        [Fact]
        public void SkillPromptRequiresAndReadsContextPolicy()
        {
            var invalid = SkillPromptService.ValidateContent("## 目的\nX\n## 出力JSON schema\n{}\n## 禁止事項\n削除");
            Assert.Contains(invalid, x => x.Contains("Context Policy"));
            var content = "## 目的\nX\n## 出力JSON schema\n{}\n## 禁止事項\n削除\n## 例\n{}\n## Context Policy\n- include_geometry: true\n- include_notes: false\n- recent_chat_messages: 3";
            Assert.Empty(SkillPromptService.ValidateContent(content));
            var policy = SkillPromptService.ParseContextPolicy(content);
            Assert.True(policy.IncludeGeometry);
            Assert.False(policy.IncludeNotes);
            Assert.Equal(3, policy.RecentChatMessages);
        }

        [Fact]
        public void ContextAssemblerHonorsBudgetAndSkillPolicy()
        {
            var slides = new List<SlideModel>();
            for (var i = 1; i <= 8; i++) slides.Add(new SlideModel
            {
                SlideIndex = i, Title = "slide " + i, NotesText = new string('注', 1000),
                Shapes = new List<ShapeModel> { new ShapeModel { Id = i, Name = "shape", Type = "textbox", HasText = true, Text = new string('文', 1600), Left = 10, Top = 20, Width = 100, Height = 50 } }
            });
            var messages = new List<ChatMessage>();
            for (var i = 0; i < 10; i++) messages.Add(new ChatMessage { Role = "user", Content = "message " + i });
            var input = new SkillInput
            {
                Context = new DeckContext { RawText = new string('背', 1000) }, Target = new TargetSelector { Raw = "@all", Type = "all" },
                Instruction = "要約", Slides = slides, RecentMessages = messages
            };
            var settings = new LmStudioSettings { ContextLength = 2048, MaxTokens = 512, InputSafetyMarginTokens = 256, RecentChatMessages = 10 };
            var policy = new SkillContextPolicy { IncludeGeometry = false, IncludeNotes = false, IncludeDeckStyle = false, IncludeRecentChat = true, RecentChatMessages = 2 };
            var assembly = new ContextAssemblerService().Build(input, "deck_summary", SkillPromptService.SafetyContract, settings, policy);
            var root = JObject.Parse(assembly.UserPayload);

            Assert.True(assembly.Report.EstimatedInputTokens <= assembly.Report.InputBudgetTokens);
            Assert.True(assembly.Report.IncludedMessages <= 2);
            Assert.Null(root["slides"]?[0]?["notes"]);
            Assert.Null(root["slides"]?[0]?["shapes"]?[0]?["left"]);
            Assert.Equal("powerpoint_slide_editing_only", (string)root["task_contract"]?["domain"]);
            Assert.Equal("deck_summary", (string)root["final_response_contract"]?["skill"]);
        }

        [Fact]
        public void StructuredSchemasAreValidJsonSchemas()
        {
            Assert.Equal("object", (string)StructuredSchemas.LayoutActionPlan["type"]);
            Assert.Equal("object", (string)StructuredSchemas.FactCheckResult["type"]);
            Assert.NotNull(StructuredSchemas.LayoutActionPlan["properties"]?["actions"]?["items"]?["properties"]?["slide_plan"]);
            Assert.Equal(1, (int)StructuredSchemas.SlideContentPlan(3)["properties"]?["slides"]?["minItems"]);
            Assert.Equal(12, (int)StructuredSchemas.SlideContentPlan(3)["properties"]?["slides"]?["maxItems"]);
            Assert.Contains("create_diagram", (string)StructuredSchemas.ForSkill("native_shape_diagram")["properties"]?["actions"]?["items"]?["properties"]?["type"]?["description"]);
        }

        [Theory]
        [InlineData("process_flow", "method_flow")]
        [InlineData("title_single_focus", "title_only")]
        [InlineData("conclusion_slide", "summary")]
        [InlineData("conceptual_model", "hypothesis_model")]
        [InlineData("2 column", "title_two_column")]
        [InlineData("result_with_figure", "result_with_figure")]
        [InlineData("完全に未知の値", "title_body")]
        public void SlideContentNormalizeMapsDriftedLayoutsToValidEnum(string drifted, string expected)
        {
            var plan = new SlideContentPlan
            {
                Slides = new List<SlideContentItem>
                {
                    new SlideContentItem { Layout = drifted, Title = "T", Message = "M", Body = new List<string> { "B" }, Notes = "", CitationNote = "" }
                }
            };
            SlideContentPlanningService.Normalize(plan, null);
            Assert.Equal(expected, plan.Slides[0].Layout);
            // 正規化後はValidateを必ず通る(旗艦のスライド生成が沈黙で壊れない)
            SlideContentPlanningService.Validate(plan, null);
        }

        [Fact]
        public void SlideContentNormalizeMapsDriftedRoleCompositionVisual()
        {
            var plan = new SlideContentPlan
            {
                Slides = new List<SlideContentItem>
                {
                    new SlideContentItem { Layout = "title_body", SlideRole = "conclusion", Composition = "text_heavy", VisualType = "pathway_diagram", Title = "T", Message = "M", Body = new List<string> { "B" }, Notes = "", CitationNote = "" }
                }
            };
            SlideContentPlanningService.Normalize(plan, null);
            Assert.Equal("summary", plan.Slides[0].SlideRole);
            Assert.Equal("single_focus", plan.Slides[0].Composition);
            Assert.Equal("native_diagram", plan.Slides[0].VisualType);
        }

        [Fact]
        public void SlideContentValidationRejectsWrongCountAndBadLayout()
        {
            var plan = new SlideContentPlan
            {
                Slides = new List<SlideContentItem> { new SlideContentItem { Layout = "title_body", Title = "A", Message = "M", Body = new List<string> { "B" }, Notes = "", CitationNote = "" } }
            };
            Assert.Throws<InvalidOperationException>(() => SlideContentPlanningService.Validate(plan, 3));
            plan.Slides[0].Layout = "unknown";
            Assert.Throws<InvalidOperationException>(() => SlideContentPlanningService.Validate(plan, null));
        }

        [Fact]
        public void SkillPolicyIsAdvisoryRatherThanSchemaRestriction()
        {
            Assert.Contains("create_slide", SkillActionPolicyService.GetPreferredActions("research_slide_writer"));
            var actionType = StructuredSchemas.ForSkill("research_slide_writer")["properties"]?["actions"]?["items"]?["properties"]?["type"];
            Assert.Null(actionType?["enum"]);
            Assert.Contains("Preferred", (string)actionType?["description"]);
        }

        [Fact]
        public void ActionPlanReadsSnakeCaseFields()
        {
            var plan = JsonUtils.Deserialize<LayoutActionPlan>("{\"skill\":\"slide_generate\",\"target\":\"@current\",\"summary\":\"x\",\"issues\":[],\"actions\":[{\"type\":\"create_slide\",\"slide\":1,\"shape_id\":12,\"slide_plan\":{\"insert_after\":1,\"layout\":\"title_body\",\"title\":\"T\",\"citation_note\":\"C\"}}]}");
            var action = Assert.Single(plan.Actions);
            Assert.Equal(12, action.ShapeId);
            Assert.Equal(1, action.SlidePlan.InsertAfter);
            Assert.Equal("C", action.SlidePlan.CitationNote);
        }

        [Fact]
        public async System.Threading.Tasks.Task NullStructuredOutputRegeneratesOriginalRequest()
        {
            using (var client = new LmStudioClient(new LmStudioSettings { StructuredOutputRetryCount = 1 }))
            {
                var service = new StructuredOutputService();
                var plan = await service.ParseActionPlanAsync("null", client, System.Threading.CancellationToken.None,
                    token => System.Threading.Tasks.Task.FromResult("{\"skill\":\"slide_generate\",\"target\":\"@current\",\"summary\":\"ok\",\"issues\":[],\"actions\":[]}"));
                Assert.Equal("slide_generate", plan.Skill);
            }
        }

        [Fact]
        public void RuleBasedLayoutAnalysisFindsDeterministicProblems()
        {
            var slide = new SlideModel
            {
                SlideIndex = 1, Width = 960, Height = 540,
                Shapes = new List<ShapeModel>
                {
                    new ShapeModel { Id = 1, Left = -5, Top = 20, Width = 100, Height = 60, Bounds = new RectModel { Left = -5, Top = 20, Width = 100, Height = 60 } },
                    new ShapeModel { Id = 2, Left = 200, Top = 100, Width = 200, Height = 100, Bounds = new RectModel { Left = 200, Top = 100, Width = 200, Height = 100 }, HasText = true, Text = new string('文', 400), FontSize = 10 },
                    new ShapeModel { Id = 3, Left = 300, Top = 130, Width = 180, Height = 90, Bounds = new RectModel { Left = 300, Top = 130, Width = 180, Height = 90 } }
                }
            };
            var issues = new LayoutAnalysisService().Analyze(new[] { slide });
            Assert.Contains(issues, x => x.IssueType == "out_of_slide" && x.ShapeIds.Contains(1));
            Assert.Contains(issues, x => x.IssueType == "unreadable_text" && x.ShapeIds.Contains(2));
            Assert.Contains(issues, x => x.IssueType == "text_density" && x.ShapeIds.Contains(2));
            Assert.Contains(issues, x => x.IssueType == "overlap" && x.ShapeIds.Contains(2) && x.ShapeIds.Contains(3));
        }

        private static string CreateTempDirectory()
        {
            var path = Path.Combine(Path.GetTempPath(), "LocalPptSkillAgent.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}

$ErrorActionPreference = 'Stop'
$bin = Join-Path $PSScriptRoot '..\bin\Debug\net48'
[void][Reflection.Assembly]::LoadFrom((Join-Path $bin 'Office.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $bin 'Microsoft.Office.Interop.PowerPoint.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $bin 'Newtonsoft.Json.dll'))
[void][Reflection.Assembly]::LoadFrom((Join-Path $bin 'LocalPptSkillAgent.dll'))

$app = $null
$presentation = $null
$comAddIn = $null
$sessionPath = $null
$failureSessionPath = $null
$multiSessionPath = $null
try {
    $app = New-Object -ComObject PowerPoint.Application
    $comAddIn = $app.COMAddIns.Item('LocalPptSkillAgent.Vsto')
    if (-not $comAddIn.Connect) { throw 'LocalPptSkillAgent VSTO add-in is registered but not connected.' }
    $presentation = $app.Presentations.Add(-1)
    $app.WindowState = 2
    $slide = $presentation.Slides.Add(1, 12)
    if ($presentation.Windows.Count -gt 0) { $presentation.Windows[1].Activate() }
    $shape = $slide.Shapes.AddTextbox(1, 100, 100, 240, 60)
    $shape.TextFrame.TextRange.Text = 'before'

    $service = [LocalPptSkillAgent.Services.PowerPointService]::new($app)
    $deckId1 = $service.GetOrCreateDeckId()
    $deckId2 = $service.GetOrCreateDeckId()
    if ([string]::IsNullOrWhiteSpace($deckId1) -or $deckId1 -ne $deckId2) { throw 'Deck identity is not stable.' }
    $target = ([LocalPptSkillAgent.Services.SelectorParser]::new()).Parse('@slide:1')
    $plan = [LocalPptSkillAgent.Models.LayoutActionPlan]::new()
    $plan.Skill = 'visual_polish'
    $plan.Target = '@slide:1'
    $plan.Summary = 'interop smoke'

    $move = [LocalPptSkillAgent.Models.LayoutAction]::new()
    $move.Type = 'move'; $move.Slide = 1; $move.ShapeId = $shape.Id; $move.Dx = 20; $move.Dy = 0
    $text = [LocalPptSkillAgent.Models.LayoutAction]::new()
    $text.Type = 'set_text'; $text.Slide = 1; $text.ShapeId = $shape.Id; $text.Text = 'after'
    $plan.Actions.Add($move)
    $plan.Actions.Add($text)

    $executor = [LocalPptSkillAgent.Services.ActionExecutor]::new($service)
    $session = $executor.Execute($plan, $target, 'smoke')
    if ([Math]::Abs($shape.Left - 120) -gt 0.01 -or $shape.TextFrame.TextRange.Text -ne 'after') {
        throw "ActionExecutor did not apply expected changes. left=$($shape.Left) text=$($shape.TextFrame.TextRange.Text)"
    }

    $rollback = [LocalPptSkillAgent.Services.RollbackManager]::new($service)
    $rollback.Rollback($session)
    if ([Math]::Abs($shape.Left - 100) -gt 0.01 -or $shape.TextFrame.TextRange.Text -ne 'before') {
        throw 'RollbackManager did not restore the original shape state.'
    }

    $failurePlan = [LocalPptSkillAgent.Models.LayoutActionPlan]::new()
    $failurePlan.Skill = 'visual_polish'; $failurePlan.Target = '@slide:1'; $failurePlan.Summary = 'forced runtime failure'
    $moveAgain = [LocalPptSkillAgent.Models.LayoutAction]::new()
    $moveAgain.Type = 'move'; $moveAgain.Slide = 1; $moveAgain.ShapeId = $shape.Id; $moveAgain.Dx = 30
    $failurePlan.Actions.Add($moveAgain)
    $transactions = [LocalPptSkillAgent.Services.TransactionManager]::new($service)
    $failureSession = $transactions.Begin($failurePlan, 'forced failure')
    $shape.Left = $shape.Left + 30
    $transactions.Abort($failureSession, $null, [InvalidOperationException]::new('forced failure'))
    if ([Math]::Abs($shape.Left - 100) -gt 0.01) { throw 'Failed transaction was not restored automatically.' }
    $failureSession = [LocalPptSkillAgent.Utils.FileStoreUtils]::ListSessions() | Where-Object Status -like 'failed_*' | Select-Object -First 1
    if (-not $failureSession -or -not $failureSession.RolledBack) { throw 'Failed transaction audit session was not saved.' }
    $failureSessionPath = Join-Path ([LocalPptSkillAgent.Utils.FileStoreUtils]::Sessions) ("session_{0}.json" -f $failureSession.SessionId)

    $multiPlan = [LocalPptSkillAgent.Models.LayoutActionPlan]::new()
    $multiPlan.Skill = 'slide_generate'; $multiPlan.Target = '@slide:1'; $multiPlan.Summary = 'three ordered slides'
    $slideSpecs = @(
        @{ Title='First'; Layout='title_two_column'; Composition='text_visual_split'; Visual='comparison'; Body="• Left`r`n• Right" },
        @{ Title='Second'; Layout='method_flow'; Composition='three_step'; Visual='process'; Body="• Step 1`r`n• Step 2`r`n• Step 3" },
        @{ Title='Third'; Layout='summary'; Composition='three_cards'; Visual='key_takeaway'; Body="• Point 1`r`n• Point 2`r`n• Point 3" }
    )
    foreach ($spec in $slideSpecs) {
        $create = [LocalPptSkillAgent.Models.LayoutAction]::new(); $create.Type = 'create_slide'; $create.Slide = 1
        $slidePlan = [LocalPptSkillAgent.Models.SlideGenerationPlan]::new(); $slidePlan.InsertAfter = 1; $slidePlan.Layout = $spec.Layout; $slidePlan.Title = $spec.Title
        $slidePlan.Message = 'Message'; $slidePlan.Body = $spec.Body; $slidePlan.Composition = $spec.Composition; $slidePlan.VisualType = $spec.Visual
        $create.SlidePlan = $slidePlan; $multiPlan.Actions.Add($create)
    }
    $multiSession = $executor.Execute($multiPlan, $target, 'three slides')
    if ($presentation.Slides.Count -ne 4 -or $presentation.Slides[2].Shapes[1].TextFrame.TextRange.Text -ne 'First' -or $presentation.Slides[4].Shapes[1].TextFrame.TextRange.Text -ne 'Third') {
        throw 'Multiple generated slides were not inserted in request order.'
    }
    if ($presentation.Slides[2].Shapes.Count -lt 3 -or $presentation.Slides[3].Shapes.Count -lt 5 -or $presentation.Slides[4].Shapes.Count -lt 5) {
        throw 'Layout-aware slide rendering did not create the expected native shape composition.'
    }
    $rollback.Rollback($multiSession)
    if ($presentation.Slides.Count -ne 1) { throw 'Multiple generated slides were not rolled back.' }
    $multiSessionPath = Join-Path ([LocalPptSkillAgent.Utils.FileStoreUtils]::Sessions) ("session_{0}.json" -f $multiSession.SessionId)
    $sessionPath = Join-Path ([LocalPptSkillAgent.Utils.FileStoreUtils]::Sessions) ("session_{0}.json" -f $session.SessionId)
    "PASS addin-connected deck-id transaction-rollback session=$($session.SessionId) shape=$($shape.Id)"
}
finally {
    if ($presentation) { try { $presentation.Close() } catch {} }
    if ($app) { try { $app.Quit() } catch {} }
    if ($sessionPath -and (Test-Path -LiteralPath $sessionPath)) { Remove-Item -LiteralPath $sessionPath -Force }
    if ($failureSessionPath -and (Test-Path -LiteralPath $failureSessionPath)) { Remove-Item -LiteralPath $failureSessionPath -Force }
    if ($multiSessionPath -and (Test-Path -LiteralPath $multiSessionPath)) { Remove-Item -LiteralPath $multiSessionPath -Force }
    foreach ($value in @($shape, $slide, $presentation, $comAddIn, $app)) {
        if ($value -and [Runtime.InteropServices.Marshal]::IsComObject($value)) {
            try { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($value) } catch {}
        }
    }
    [GC]::Collect()
    [GC]::WaitForPendingFinalizers()
}

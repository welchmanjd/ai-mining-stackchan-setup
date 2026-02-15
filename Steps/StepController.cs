using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AiStackchanSetup.ViewModels;
using Serilog;

namespace AiStackchanSetup.Steps;

public sealed class StepController
{
    private readonly MainViewModel _viewModel;
    private readonly StepContext _context;
    private readonly List<IStep> _steps;
    private IStep? _current;

    public StepController(MainViewModel viewModel, StepContext context, IEnumerable<IStep> steps)
    {
        _viewModel = viewModel;
        _context = context;
        _steps = steps.OrderBy(s => s.Index).ToList();
        if (_steps.Count == 0)
        {
            throw new InvalidOperationException("Steps are not configured.");
        }
    }

    public int TotalSteps => _steps.Count;

    public IStep CurrentStep => _current ??= GetStep(_viewModel.Step);

    public void SyncStepMetadata()
    {
        var step = GetStep(_viewModel.Step);
        _current = step;
        _viewModel.StepTitle = step.Title;
        _viewModel.StepDescription = step.Description;
        _viewModel.PrimaryButtonText = step.PrimaryActionText;
    }

    public async Task<StepResult> ExecuteCurrentAsync(CancellationToken token)
    {
        var step = GetStep(_viewModel.Step);
        _current = step;
        _viewModel.ErrorMessage = "";

        step.OnEnter(_context);
        try
        {
            Log.Information("Step start {Index}:{Title}", step.Index, step.Title);
            var result = await step.ExecuteAsync(_context, token);
            Log.Information("Step end {Index}:{Title} status={Status} canRetry={CanRetry} error={Error}",
                step.Index, step.Title, result.Status, result.CanRetry, result.ErrorMessage);
            return result;
        }
        finally
        {
            step.OnLeave(_context);
        }
    }

    public void MoveNext()
    {
        if (_viewModel.Step < _steps.Count)
        {
            _viewModel.Step++;
        }
    }

    public void MovePrevious()
    {
        if (_viewModel.Step > 1)
        {
            _viewModel.Step--;
        }
    }

    public bool CanSkip => CurrentStep.CanSkip;

    public void Skip()
    {
        if (CurrentStep.CanSkip)
        {
            MoveNext();
        }
    }

    private IStep GetStep(int index)
    {
        var step = _steps.FirstOrDefault(s => s.Index == index);
        if (step == null)
        {
            throw new InvalidOperationException($"Step not found: {index}");
        }

        return step;
    }
}

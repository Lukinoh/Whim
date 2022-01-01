using System;
using System.Windows.Input;

namespace Whim.Bar;

public class NextLayoutEngineCommand : System.Windows.Input.ICommand
{
	private readonly IConfigContext _configContext;
	private readonly ActiveLayoutWidgetViewModel _viewModel;

	public NextLayoutEngineCommand(IConfigContext configContext, ActiveLayoutWidgetViewModel viewModel)
	{
		_configContext = configContext;
		_viewModel = viewModel;
	}

	public event EventHandler? CanExecuteChanged
	{
		add => CommandManager.RequerySuggested += value;
		remove => CommandManager.RequerySuggested -= value;
	}

	public bool CanExecute(object? parameter) => true;

	public void Execute(object? parameter)
	{
		Logger.Debug("Switching to next layout engine");
		_configContext.WorkspaceManager.GetWorkspaceForMonitor(_viewModel.Monitor)?.NextLayoutEngine();
	}
}

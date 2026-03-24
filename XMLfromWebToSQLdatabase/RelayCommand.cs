using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace XMLfromWebToSQLdatabase
{
    internal class RelayCommand : ICommand
    {
        private readonly Func<Task> _execute;   // Action to execute when the command is invoked
        private readonly Func<bool>? _canExecute;   // Optional function to determine if the command can execute

        public RelayCommand(Func<Task> execute, Func<bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged;

        public async void Execute(object? parameter) => await _execute();
        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;
        public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}

using Avalonia.Controls;
using Avalonia.Controls.DataGridEditing;
using Avalonia.Input;

namespace Avalonia.Plugin.ProDataGrid.EditingModels;

public sealed class DoubleClickOnlyEditingInteractionModel : DataGridEditingInteractionModel
{
    public override bool ShouldBeginEditOnPointer(DataGridPointerEditContext context)
    {
        if (context.EditTriggers == DataGridEditTriggers.None)
            return false;

        if (context.IsDoubleClick)
            return context.EditTriggers.HasFlag(DataGridEditTriggers.CellDoubleClick) ||
                   context.EditTriggers.HasFlag(DataGridEditTriggers.CellClick);

        return false;
    }
}

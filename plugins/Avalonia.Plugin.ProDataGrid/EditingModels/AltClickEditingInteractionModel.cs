using Avalonia.Controls;
using Avalonia.Controls.DataGridEditing;
using Avalonia.Input;

namespace Avalonia.Plugin.ProDataGrid.EditingModels;

public sealed class AltClickEditingInteractionModel : DataGridEditingInteractionModel
{
    public override bool ShouldBeginEditOnPointer(DataGridPointerEditContext context)
    {
        if (context.EditTriggers == DataGridEditTriggers.None)
            return false;

        if (context.IsDoubleClick)
        {
            if (!context.EditTriggers.HasFlag(DataGridEditTriggers.CellDoubleClick) &&
                !context.EditTriggers.HasFlag(DataGridEditTriggers.CellClick))
                return false;
        }
        else if (!context.EditTriggers.HasFlag(DataGridEditTriggers.CellClick))
        {
            return false;
        }

        return context.Modifiers.HasFlag(KeyModifiers.Alt);
    }
}

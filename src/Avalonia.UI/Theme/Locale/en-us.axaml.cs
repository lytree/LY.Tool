using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Plugin.Shared.Resources;

namespace Avalonia.UI.Theme.Locale;

public class en_us : ResourceDictionary
{
    public en_us()
    {
        AvaloniaXamlLoader.Load(this);
        this["STRING_MENU_BRING_TO_FRONT"] = Strings.MENU_BRING_TO_FRONT;
        this["STRING_MENU_BRING_FORWARD"] = Strings.MENU_BRING_FORWARD;
        this["STRING_MENU_SEND_BACKWARD"] = Strings.MENU_SEND_BACKWARD;
        this["STRING_MENU_SEND_TO_BACK"] = Strings.MENU_SEND_TO_BACK;
        this["STRING_MENU_DIALOG_OK"] = Strings.MENU_DIALOG_OK;
        this["STRING_MENU_DIALOG_CANCEL"] = Strings.MENU_DIALOG_CANCEL;
        this["STRING_MENU_DIALOG_YES"] = Strings.MENU_DIALOG_YES;
        this["STRING_MENU_DIALOG_NO"] = Strings.MENU_DIALOG_NO;
        this["STRING_MENU_DIALOG_CLOSE"] = Strings.MENU_DIALOG_CLOSE;
        this["STRING_MENU_CUT"] = Strings.MENU_CUT;
        this["STRING_MENU_COPY"] = Strings.MENU_COPY;
        this["STRING_MENU_PASTE"] = Strings.MENU_PASTE;
        this["STRING_MENU_CLEAR"] = Strings.MENU_CLEAR;
        this["STRING_PAGINATION_JUMP_TO"] = Strings.PAGINATION_JUMP_TO;
        this["STRING_PAGINATION_PAGE"] = string.Empty;
        this["STRING_THEME_TOGGLE_DARK"] = Strings.THEME_TOGGLE_DARK;
        this["STRING_THEME_TOGGLE_LIGHT"] = Strings.THEME_TOGGLE_LIGHT;
        this["STRING_THEME_TOGGLE_SYSTEM"] = Strings.THEME_TOGGLE_SYSTEM;
        this["STRING_DATE_TIME_CONFIRM"] = Strings.DATE_TIME_CONFIRM;
        this["STRING_DATE_TIME_START_TIME"] = Strings.DATE_TIME_START_TIME;
        this["STRING_DATE_TIME_END_TIME"] = Strings.DATE_TIME_END_TIME;
        this["STRING_CHOOSER_DIALOG_OK"] = Strings.CHOOSER_DIALOG_OK;
        this["STRING_CHOOSER_DIALOG_CANCEL"] = Strings.CHOOSER_DIALOG_CANCEL;
        this["STRING_CHOOSER_FILE_NAME"] = Strings.CHOOSER_FILE_NAME;
        this["STRING_CHOOSER_SHOW_HIDDEN_FILES"] = Strings.CHOOSER_SHOW_HIDDEN_FILES;
        this["STRING_CHOOSER_NAME_COLUMN"] = Strings.CHOOSER_NAME_COLUMN;
        this["STRING_CHOOSER_DATEMODIFIED_COLUMN"] = Strings.CHOOSER_DATEMODIFIED_COLUMN;
        this["STRING_CHOOSER_TYPE_COLUMN"] = Strings.CHOOSER_TYPE_COLUMN;
        this["STRING_CHOOSER_SIZE_COLUMN"] = Strings.CHOOSER_SIZE_COLUMN;
        this["STRING_CHOOSER_PROMPT_FILE_ALREADY_EXISTS"] = Strings.CHOOSER_PROMPT_FILE_ALREADY_EXISTS;
        this["STRING_DRAWERPAGE_TOGGLE_NAVIGATION_DRAWER"] = Strings.DRAWERPAGE_TOGGLE_NAVIGATION_DRAWER;
        this["STRING_BREADCRUMB_HOME"] = Strings.BREADCRUMB_HOME;
    }
}

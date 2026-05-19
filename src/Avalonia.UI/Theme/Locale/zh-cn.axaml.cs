using System.Globalization;
using Avalonia.Controls;
using Avalonia.Plugin.Shared.Resources;

namespace Avalonia.UI.Theme.Locale;

public class zh_cn : ResourceDictionary
{
    public zh_cn()
    {
        var culture = new CultureInfo("zh-CN");
        this["STRING_MENU_BRING_TO_FRONT"] = Strings.ResourceManager.GetString(nameof(Strings.MENU_BRING_TO_FRONT), culture)!;
        this["STRING_MENU_BRING_FORWARD"] = Strings.ResourceManager.GetString(nameof(Strings.MENU_BRING_FORWARD), culture)!;
        this["STRING_MENU_SEND_BACKWARD"] = Strings.ResourceManager.GetString(nameof(Strings.MENU_SEND_BACKWARD), culture)!;
        this["STRING_MENU_SEND_TO_BACK"] = Strings.ResourceManager.GetString(nameof(Strings.MENU_SEND_TO_BACK), culture)!;
        this["STRING_MENU_DIALOG_OK"] = Strings.ResourceManager.GetString(nameof(Strings.MENU_DIALOG_OK), culture)!;
        this["STRING_MENU_DIALOG_CANCEL"] = Strings.ResourceManager.GetString(nameof(Strings.MENU_DIALOG_CANCEL), culture)!;
        this["STRING_MENU_DIALOG_YES"] = Strings.ResourceManager.GetString(nameof(Strings.MENU_DIALOG_YES), culture)!;
        this["STRING_MENU_DIALOG_NO"] = Strings.ResourceManager.GetString(nameof(Strings.MENU_DIALOG_NO), culture)!;
        this["STRING_MENU_DIALOG_CLOSE"] = Strings.ResourceManager.GetString(nameof(Strings.MENU_DIALOG_CLOSE), culture)!;
        this["STRING_MENU_CUT"] = Strings.ResourceManager.GetString(nameof(Strings.MENU_CUT), culture)!;
        this["STRING_MENU_COPY"] = Strings.ResourceManager.GetString(nameof(Strings.MENU_COPY), culture)!;
        this["STRING_MENU_PASTE"] = Strings.ResourceManager.GetString(nameof(Strings.MENU_PASTE), culture)!;
        this["STRING_MENU_CLEAR"] = Strings.ResourceManager.GetString(nameof(Strings.MENU_CLEAR), culture)!;
        this["STRING_PAGINATION_JUMP_TO"] = Strings.ResourceManager.GetString(nameof(Strings.PAGINATION_JUMP_TO), culture)!;
        this["STRING_PAGINATION_PAGE"] = Strings.ResourceManager.GetString(nameof(Strings.PAGINATION_PAGE), culture)!;
        this["STRING_THEME_TOGGLE_DARK"] = Strings.ResourceManager.GetString(nameof(Strings.THEME_TOGGLE_DARK), culture)!;
        this["STRING_THEME_TOGGLE_LIGHT"] = Strings.ResourceManager.GetString(nameof(Strings.THEME_TOGGLE_LIGHT), culture)!;
        this["STRING_THEME_TOGGLE_SYSTEM"] = Strings.ResourceManager.GetString(nameof(Strings.THEME_TOGGLE_SYSTEM), culture)!;
        this["STRING_DATE_TIME_CONFIRM"] = Strings.ResourceManager.GetString(nameof(Strings.DATE_TIME_CONFIRM), culture)!;
        this["STRING_DATE_TIME_START_TIME"] = Strings.ResourceManager.GetString(nameof(Strings.DATE_TIME_START_TIME), culture)!;
        this["STRING_DATE_TIME_END_TIME"] = Strings.ResourceManager.GetString(nameof(Strings.DATE_TIME_END_TIME), culture)!;
        this["STRING_CHOOSER_DIALOG_OK"] = Strings.ResourceManager.GetString(nameof(Strings.CHOOSER_DIALOG_OK), culture)!;
        this["STRING_CHOOSER_DIALOG_CANCEL"] = Strings.ResourceManager.GetString(nameof(Strings.CHOOSER_DIALOG_CANCEL), culture)!;
        this["STRING_CHOOSER_FILE_NAME"] = Strings.ResourceManager.GetString(nameof(Strings.CHOOSER_FILE_NAME), culture)!;
        this["STRING_CHOOSER_SHOW_HIDDEN_FILES"] = Strings.ResourceManager.GetString(nameof(Strings.CHOOSER_SHOW_HIDDEN_FILES), culture)!;
        this["STRING_CHOOSER_NAME_COLUMN"] = Strings.ResourceManager.GetString(nameof(Strings.CHOOSER_NAME_COLUMN), culture)!;
        this["STRING_CHOOSER_DATEMODIFIED_COLUMN"] = Strings.ResourceManager.GetString(nameof(Strings.CHOOSER_DATEMODIFIED_COLUMN), culture)!;
        this["STRING_CHOOSER_TYPE_COLUMN"] = Strings.ResourceManager.GetString(nameof(Strings.CHOOSER_TYPE_COLUMN), culture)!;
        this["STRING_CHOOSER_SIZE_COLUMN"] = Strings.ResourceManager.GetString(nameof(Strings.CHOOSER_SIZE_COLUMN), culture)!;
        this["STRING_CHOOSER_PROMPT_FILE_ALREADY_EXISTS"] = Strings.ResourceManager.GetString(nameof(Strings.CHOOSER_PROMPT_FILE_ALREADY_EXISTS), culture)!;
        this["STRING_DRAWERPAGE_TOGGLE_NAVIGATION_DRAWER"] = Strings.ResourceManager.GetString(nameof(Strings.DRAWERPAGE_TOGGLE_NAVIGATION_DRAWER), culture)!;
        this["STRING_BREADCRUMB_HOME"] = Strings.ResourceManager.GetString(nameof(Strings.BREADCRUMB_HOME), culture)!;
    }
}

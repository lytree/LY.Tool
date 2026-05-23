#nullable enable
namespace Avalonia.UI.Resources;

public static class Strings
{
    private static global::System.Resources.ResourceManager? _resourceManager;

    public static global::System.Resources.ResourceManager ResourceManager
    {
        get
        {
            if (_resourceManager is null)
            {
                _resourceManager = new global::System.Resources.ResourceManager(
                    "Avalonia.UI.Resources.Strings",
                    typeof(Strings).Assembly);
            }
            return _resourceManager;
        }
    }

    public static global::System.Globalization.CultureInfo Culture
    {
        get => global::System.Globalization.CultureInfo.CurrentUICulture;
        set => global::System.Globalization.CultureInfo.CurrentUICulture = value;
    }

    public static string MENU_BRING_TO_FRONT => ResourceManager.GetString(nameof(MENU_BRING_TO_FRONT), Culture)!;
    public static string MENU_BRING_FORWARD => ResourceManager.GetString(nameof(MENU_BRING_FORWARD), Culture)!;
    public static string MENU_SEND_BACKWARD => ResourceManager.GetString(nameof(MENU_SEND_BACKWARD), Culture)!;
    public static string MENU_SEND_TO_BACK => ResourceManager.GetString(nameof(MENU_SEND_TO_BACK), Culture)!;
    public static string MENU_DIALOG_OK => ResourceManager.GetString(nameof(MENU_DIALOG_OK), Culture)!;
    public static string MENU_DIALOG_CANCEL => ResourceManager.GetString(nameof(MENU_DIALOG_CANCEL), Culture)!;
    public static string MENU_DIALOG_YES => ResourceManager.GetString(nameof(MENU_DIALOG_YES), Culture)!;
    public static string MENU_DIALOG_NO => ResourceManager.GetString(nameof(MENU_DIALOG_NO), Culture)!;
    public static string MENU_DIALOG_CLOSE => ResourceManager.GetString(nameof(MENU_DIALOG_CLOSE), Culture)!;
    public static string MENU_CUT => ResourceManager.GetString(nameof(MENU_CUT), Culture)!;
    public static string MENU_COPY => ResourceManager.GetString(nameof(MENU_COPY), Culture)!;
    public static string MENU_PASTE => ResourceManager.GetString(nameof(MENU_PASTE), Culture)!;
    public static string MENU_CLEAR => ResourceManager.GetString(nameof(MENU_CLEAR), Culture)!;
    public static string PAGINATION_JUMP_TO => ResourceManager.GetString(nameof(PAGINATION_JUMP_TO), Culture)!;
    public static string PAGINATION_PAGE => ResourceManager.GetString(nameof(PAGINATION_PAGE), Culture)!;
    public static string THEME_TOGGLE_DARK => ResourceManager.GetString(nameof(THEME_TOGGLE_DARK), Culture)!;
    public static string THEME_TOGGLE_LIGHT => ResourceManager.GetString(nameof(THEME_TOGGLE_LIGHT), Culture)!;
    public static string THEME_TOGGLE_SYSTEM => ResourceManager.GetString(nameof(THEME_TOGGLE_SYSTEM), Culture)!;
    public static string DATE_TIME_CONFIRM => ResourceManager.GetString(nameof(DATE_TIME_CONFIRM), Culture)!;
    public static string DATE_TIME_START_TIME => ResourceManager.GetString(nameof(DATE_TIME_START_TIME), Culture)!;
    public static string DATE_TIME_END_TIME => ResourceManager.GetString(nameof(DATE_TIME_END_TIME), Culture)!;
    public static string CHOOSER_DIALOG_OK => ResourceManager.GetString(nameof(CHOOSER_DIALOG_OK), Culture)!;
    public static string CHOOSER_DIALOG_CANCEL => ResourceManager.GetString(nameof(CHOOSER_DIALOG_CANCEL), Culture)!;
    public static string CHOOSER_FILE_NAME => ResourceManager.GetString(nameof(CHOOSER_FILE_NAME), Culture)!;
    public static string CHOOSER_SHOW_HIDDEN_FILES => ResourceManager.GetString(nameof(CHOOSER_SHOW_HIDDEN_FILES), Culture)!;
    public static string CHOOSER_NAME_COLUMN => ResourceManager.GetString(nameof(CHOOSER_NAME_COLUMN), Culture)!;
    public static string CHOOSER_DATEMODIFIED_COLUMN => ResourceManager.GetString(nameof(CHOOSER_DATEMODIFIED_COLUMN), Culture)!;
    public static string CHOOSER_TYPE_COLUMN => ResourceManager.GetString(nameof(CHOOSER_TYPE_COLUMN), Culture)!;
    public static string CHOOSER_SIZE_COLUMN => ResourceManager.GetString(nameof(CHOOSER_SIZE_COLUMN), Culture)!;
    public static string CHOOSER_PROMPT_FILE_ALREADY_EXISTS => ResourceManager.GetString(nameof(CHOOSER_PROMPT_FILE_ALREADY_EXISTS), Culture)!;
    public static string DRAWERPAGE_TOGGLE_NAVIGATION_DRAWER => ResourceManager.GetString(nameof(DRAWERPAGE_TOGGLE_NAVIGATION_DRAWER), Culture)!;
    public static string BREADCRUMB_HOME => ResourceManager.GetString(nameof(BREADCRUMB_HOME), Culture)!;
    public static string NAV_Introduction => ResourceManager.GetString(nameof(NAV_Introduction), Culture)!;
    public static string NAV_Settings => ResourceManager.GetString(nameof(NAV_Settings), Culture)!;
    public static string NAV_Plugins => ResourceManager.GetString(nameof(NAV_Plugins), Culture)!;
    public static string NAV_PluginManagement => ResourceManager.GetString(nameof(NAV_PluginManagement), Culture)!;
    public static string NAV_AboutUs => ResourceManager.GetString(nameof(NAV_AboutUs), Culture)!;
    public static string PLUGIN_MANAGEMENT_TITLE => ResourceManager.GetString(nameof(PLUGIN_MANAGEMENT_TITLE), Culture)!;
    public static string PLUGIN_MANAGEMENT_DESC => ResourceManager.GetString(nameof(PLUGIN_MANAGEMENT_DESC), Culture)!;
    public static string INSTALL_PLUGIN => ResourceManager.GetString(nameof(INSTALL_PLUGIN), Culture)!;
    public static string REFRESH => ResourceManager.GetString(nameof(REFRESH), Culture)!;
    public static string ENABLE => ResourceManager.GetString(nameof(ENABLE), Culture)!;
    public static string DISABLE => ResourceManager.GetString(nameof(DISABLE), Culture)!;
    public static string UNINSTALL => ResourceManager.GetString(nameof(UNINSTALL), Culture)!;
    public static string VERSION_LABEL => ResourceManager.GetString(nameof(VERSION_LABEL), Culture)!;
    public static string AUTHOR_LABEL => ResourceManager.GetString(nameof(AUTHOR_LABEL), Culture)!;
    public static string NO_PLUGINS_INSTALLED => ResourceManager.GetString(nameof(NO_PLUGINS_INSTALLED), Culture)!;
    public static string CLICK_INSTALL_PLUGIN => ResourceManager.GetString(nameof(CLICK_INSTALL_PLUGIN), Culture)!;
    public static string SELECT_PLUGIN_PACKAGE => ResourceManager.GetString(nameof(SELECT_PLUGIN_PACKAGE), Culture)!;
    public static string PLUGIN_PACKAGE => ResourceManager.GetString(nameof(PLUGIN_PACKAGE), Culture)!;
    public static string STATE_LOADED => ResourceManager.GetString(nameof(STATE_LOADED), Culture)!;
    public static string STATE_INSTALLED => ResourceManager.GetString(nameof(STATE_INSTALLED), Culture)!;
    public static string STATE_DISABLED => ResourceManager.GetString(nameof(STATE_DISABLED), Culture)!;
    public static string STATE_PENDING_UNINSTALL => ResourceManager.GetString(nameof(STATE_PENDING_UNINSTALL), Culture)!;
    public static string STATE_ERROR => ResourceManager.GetString(nameof(STATE_ERROR), Culture)!;
    public static string STATE_NOT_INSTALLED => ResourceManager.GetString(nameof(STATE_NOT_INSTALLED), Culture)!;
    public static string PLUGIN_INSTALLED_SUCCESS => ResourceManager.GetString(nameof(PLUGIN_INSTALLED_SUCCESS), Culture)!;
    public static string INSTALLATION_FAILED => ResourceManager.GetString(nameof(INSTALLATION_FAILED), Culture)!;
    public static string PLUGIN_UNINSTALL_AFTER_RESTART => ResourceManager.GetString(nameof(PLUGIN_UNINSTALL_AFTER_RESTART), Culture)!;
    public static string SETTINGS_TITLE => ResourceManager.GetString(nameof(SETTINGS_TITLE), Culture)!;
    public static string RESET => ResourceManager.GetString(nameof(RESET), Culture)!;
    public static string SAVE => ResourceManager.GetString(nameof(SAVE), Culture)!;
    public static string SAVED => ResourceManager.GetString(nameof(SAVED), Culture)!;
    public static string SAVE_FAILED => ResourceManager.GetString(nameof(SAVE_FAILED), Culture)!;
    public static string ABOUT => ResourceManager.GetString(nameof(ABOUT), Culture)!;
    public static string APP_TITLE => ResourceManager.GetString(nameof(APP_TITLE), Culture)!;
    public static string APP_VERSION => ResourceManager.GetString(nameof(APP_VERSION), Culture)!;
    public static string APP_DESCRIPTION => ResourceManager.GetString(nameof(APP_DESCRIPTION), Culture)!;
    public static string BROWSE => ResourceManager.GetString(nameof(BROWSE), Culture)!;
    public static string SELECT_FILE_PATH => ResourceManager.GetString(nameof(SELECT_FILE_PATH), Culture)!;
    public static string SETTING_THEME => ResourceManager.GetString(nameof(SETTING_THEME), Culture)!;
    public static string SETTING_THEME_DESC => ResourceManager.GetString(nameof(SETTING_THEME_DESC), Culture)!;
    public static string SETTING_LANGUAGE => ResourceManager.GetString(nameof(SETTING_LANGUAGE), Culture)!;
    public static string SETTING_LANGUAGE_DESC => ResourceManager.GetString(nameof(SETTING_LANGUAGE_DESC), Culture)!;
    public static string SETTING_COLLAPSE_SIDEBAR => ResourceManager.GetString(nameof(SETTING_COLLAPSE_SIDEBAR), Culture)!;
    public static string SETTING_COLLAPSE_SIDEBAR_DESC => ResourceManager.GetString(nameof(SETTING_COLLAPSE_SIDEBAR_DESC), Culture)!;
    public static string SETTING_USER_NAME => ResourceManager.GetString(nameof(SETTING_USER_NAME), Culture)!;
    public static string SETTING_USER_NAME_DESC => ResourceManager.GetString(nameof(SETTING_USER_NAME_DESC), Culture)!;
    public static string GROUP_APPEARANCE => ResourceManager.GetString(nameof(GROUP_APPEARANCE), Culture)!;
    public static string GROUP_GENERAL => ResourceManager.GetString(nameof(GROUP_GENERAL), Culture)!;
    public static string COMPANY_NAME_CN => ResourceManager.GetString(nameof(COMPANY_NAME_CN), Culture)!;
    public static string COMPANY_NAME_EN => ResourceManager.GetString(nameof(COMPANY_NAME_EN), Culture)!;
    public static string SLOGAN_CN => ResourceManager.GetString(nameof(SLOGAN_CN), Culture)!;
    public static string SLOGAN_EN => ResourceManager.GetString(nameof(SLOGAN_EN), Culture)!;
    public static string SEMI_DESC_CN => ResourceManager.GetString(nameof(SEMI_DESC_CN), Culture)!;
    public static string SEMI_DESC_EN => ResourceManager.GetString(nameof(SEMI_DESC_EN), Culture)!;
    public static string URSA_DESC_CN => ResourceManager.GetString(nameof(URSA_DESC_CN), Culture)!;
    public static string URSA_DESC_EN => ResourceManager.GetString(nameof(URSA_DESC_EN), Culture)!;
    public static string MANTRA_DESC_CN => ResourceManager.GetString(nameof(MANTRA_DESC_CN), Culture)!;
    public static string MANTRA_DESC_EN => ResourceManager.GetString(nameof(MANTRA_DESC_EN), Culture)!;
    public static string HUSKA_DESC_CN => ResourceManager.GetString(nameof(HUSKA_DESC_CN), Culture)!;
    public static string HUSKA_DESC_EN => ResourceManager.GetString(nameof(HUSKA_DESC_EN), Culture)!;
    public static string OPEN_SOURCE => ResourceManager.GetString(nameof(OPEN_SOURCE), Culture)!;
    public static string COMMERCIAL => ResourceManager.GetString(nameof(COMMERCIAL), Culture)!;
    public static string HOMEPAGE => ResourceManager.GetString(nameof(HOMEPAGE), Culture)!;
    public static string TOOLTIP_SETTINGS => ResourceManager.GetString(nameof(TOOLTIP_SETTINGS), Culture)!;
    public static string TOOLTIP_PLUGIN_MANAGEMENT => ResourceManager.GetString(nameof(TOOLTIP_PLUGIN_MANAGEMENT), Culture)!;
    public static string TOOLTIP_TOGGLE_SIDEBAR => ResourceManager.GetString(nameof(TOOLTIP_TOGGLE_SIDEBAR), Culture)!;
    public static string WINDOW_TITLE => ResourceManager.GetString(nameof(WINDOW_TITLE), Culture)!;
    public static string SPLASH_STARTING => ResourceManager.GetString(nameof(SPLASH_STARTING), Culture)!;
    public static string SPLASH_TITLE => ResourceManager.GetString(nameof(SPLASH_TITLE), Culture)!;
    public static string SPLASH_SUBTITLE => ResourceManager.GetString(nameof(SPLASH_SUBTITLE), Culture)!;
    public static string SPLASH_WELCOME => ResourceManager.GetString(nameof(SPLASH_WELCOME), Culture)!;
    public static string WELCOME_IRIHI => ResourceManager.GetString(nameof(WELCOME_IRIHI), Culture)!;
    public static string LOADING_MODULES => ResourceManager.GetString(nameof(LOADING_MODULES), Culture)!;
    public static string MENU_ABOUT_US => ResourceManager.GetString(nameof(MENU_ABOUT_US), Culture)!;
    public static string EXIT_CONFIRM_MESSAGE => ResourceManager.GetString(nameof(EXIT_CONFIRM_MESSAGE), Culture)!;
    public static string EXIT_CONFIRM_TITLE => ResourceManager.GetString(nameof(EXIT_CONFIRM_TITLE), Culture)!;
    public static string RESTART_REQUIRED => ResourceManager.GetString(nameof(RESTART_REQUIRED), Culture)!;
}

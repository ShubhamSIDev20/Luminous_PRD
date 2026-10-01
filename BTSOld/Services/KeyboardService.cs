namespace BatteryTestingSystem.Services
{
    // <summary>
    /// Service for handling keyboard shortcuts globally - Pure C#
    /// </summary>
    public class KeyboardService
    {
        private readonly PopupService _popupService;

        public KeyboardService(PopupService popupService)
        {
            _popupService = popupService;
        }

        /// <summary>
        /// Handle ESC key press
        /// Returns true if the key was handled
        /// </summary>
        public bool HandleEscapeKey()
        {
            // Try to close the topmost popup
            return _popupService.CloseTopPopup();
        }

        /// <summary>
        /// Handle keyboard shortcut
        /// </summary>
        public bool HandleShortcut(string key, bool ctrl = false, bool shift = false, bool alt = false)
        {
            // ESC key
            if (key == "Escape" && !ctrl && !shift && !alt)
            {
                return HandleEscapeKey();
            }

            // Add more shortcuts as needed
            // Example: Ctrl+K for search, Ctrl+/ for help, etc.

            return false;
        }
    }

}

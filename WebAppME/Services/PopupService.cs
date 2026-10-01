namespace BatteryTestingSystem.Services
{
    public class PopupService
    {
        private readonly List<PopupInstance> _activePopups = new();
        private int _nextZIndex = 1000;

        public event Action? OnChange;

        public PopupInstance RegisterPopup(string id, PopupType type = PopupType.Dialog, string? parentId = null)
        {
            var popup = new PopupInstance
            {
                Id = id,
                Type = type,
                ZIndex = _nextZIndex++,
                ParentId = parentId,
                OpenedAt = DateTime.Now
            };

            _activePopups.Add(popup);
            OnChange?.Invoke();
            return popup;
        }

        public void UnregisterPopup(string id)
        {
            var popup = _activePopups.FirstOrDefault(p => p.Id == id);
            if (popup != null)
            {
                _activePopups.Remove(popup);
                OnChange?.Invoke();
            }
        }

        public bool CloseTopPopup()
        {
            var topPopup = _activePopups
                .Where(p => p.CanCloseWithEsc)
                .OrderByDescending(p => p.ZIndex)
                .FirstOrDefault();

            if (topPopup != null)
            {
                topPopup.RequestClose?.Invoke();
                return true;
            }

            return false;
        }

        public void CloseAll()
        {
            var popupsToClose = _activePopups.ToList();
            foreach (var popup in popupsToClose)
            {
                popup.RequestClose?.Invoke();
            }
        }

        public int GetZIndex(string id)
        {
            var popup = _activePopups.FirstOrDefault(p => p.Id == id);
            return popup?.ZIndex ?? 1000;
        }

        public void CloseAllExcept(string id)
        {
            var keepIds = new HashSet<string>();
            var current = _activePopups.FirstOrDefault(p => p.Id == id);

            // Keep self + all parents
            if (current != null)
            {
                keepIds.Add(current.Id);
                //Console.WriteLine($"🔒 Keeping: {current.Id}");
                current = current.ParentId == null
                    ? null
                    : _activePopups.FirstOrDefault(p => p.Id == current.ParentId);
            }

            var popupsToClose = _activePopups
                .Where(p => !keepIds.Contains(p.Id))
                .ToList();

            //Console.WriteLine($"🚫 Closing {popupsToClose.Count} popups");
            foreach (var popup in popupsToClose)
            {
                //Console.WriteLine($"   Closing: {popup.Id}");
                popup.RequestClose?.Invoke();
            }
        }

        public bool HasOpenPopups => _activePopups.Any();
        public int PopupCount => _activePopups.Count;
    }

    public class PopupInstance
    {
        public string Id { get; set; } = string.Empty;
        public string? ParentId { get; set; }
        public PopupType Type { get; set; }
        public int ZIndex { get; set; }
        public DateTime OpenedAt { get; set; }
        public bool CanCloseWithEsc { get; set; } = true;
        public bool CanCloseWithOverlay { get; set; } = true;
        public Action? RequestClose { get; set; }
    }

    public enum PopupType
    {
        Dialog,
        Modal,
        AlertDialog,
        Sheet,
        Drawer,
        Popover,
        Tooltip,
        Dropdown
    }
}
using BatteryTestingSystem.Models.APIModels;

namespace BatteryTestingSystem.Utils
{
    public static class ChannelExpander
    {
        public const int MaxChannels = 128;

        /// <summary>
        /// Validates and returns the sorted, deduplicated positive channel numbers from ChannelList.
        /// </summary>
        public static List<int> GetChannels(CommonRequest request)
        {
            if (request.ChannelList == null || request.ChannelList.Count == 0)
                throw new ArgumentException("ChannelList cannot be null or empty.");

            var channels = request.ChannelList.Distinct().Where(c => c > 0).OrderBy(c => c).ToList();

            if (channels.Count == 0)
                throw new ArgumentException("ChannelList must contain at least one valid channel number (> 0).");

            if (channels.Count > MaxChannels)
                throw new ArgumentException($"Channel count {channels.Count} exceeds the maximum of {MaxChannels}.");

            return channels;
        }

        /// <summary>
        /// Expands a single CommonRequest into individual single-channel CommonRequest objects.
        /// </summary>
        public static List<CommonRequest> ExpandChannels(CommonRequest request)
        {
            var channels = GetChannels(request);

            return channels.Select(ch => new CommonRequest
            {
                DeviceID = request.DeviceID,
                SecondaryBoardNumber = request.SecondaryBoardNumber,
                ChannelList = new List<int> { ch },
                ProgramId = request.ProgramId,
                BatteryId = request.BatteryId,
                dbcId = request.dbcId,
            }).ToList();
        }
    }
}
